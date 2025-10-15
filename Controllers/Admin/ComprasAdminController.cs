using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using SistemaComercialPyme.Services;
using Stripe;

namespace SistemaComercialPyme.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class ComprasAdminController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly StripeService _stripeService;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public ComprasAdminController(
            PymeArtesaniasContext context,
            StripeService stripeService,
            EmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _stripeService = stripeService;
            _emailService = emailService;
            _configuration = configuration;
        }

        // GET: api/admin/compras
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetCompras([FromQuery] string? searchString)
        {
            var compras = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.DetalleCompras)
                    .ThenInclude(d => d.Producto)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                compras = compras.Where(c =>
                    c.Proveedor!.Nombre.Contains(searchString) ||
                    c.CompraId.ToString().Contains(searchString));
            }

            // Proyectar solo los campos necesarios y limitar la respuesta
            var result = await compras
                .OrderByDescending(c => c.Fecha)
                .Take(100) // Limitar a las últimas 100 compras
                .Select(c => new
                {
                    c.CompraId,
                    c.Fecha,
                    c.Total,
                    c.Estado,
                    c.TotalPagado,
                    saldoPendiente = c.Total - (c.TotalPagado ?? 0),
                    proveedor = new
                    {
                        c.Proveedor!.ProveedorId,
                        c.Proveedor!.Nombre,
                        c.Proveedor.Email
                    },
                    detalles = c.DetalleCompras.Select(d => new
                    {
                        d.ProductoId,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal,
                        producto = d.Producto!.Nombre
                    })
                })
                .ToListAsync();

            return Ok(result);
        }

        // GET: api/admin/compras/5
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetCompra(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.DetalleCompras)
                    .ThenInclude(d => d.Producto)
                .Where(c => c.CompraId == id)
                .Select(c => new
                {
                    c.CompraId,
                    c.Fecha,
                    c.Total,
                    c.Estado,
                    c.TotalPagado,
                    saldoPendiente = c.Total - (c.TotalPagado ?? 0),
                    proveedor = new { c.Proveedor!.ProveedorId, c.Proveedor.Nombre, c.Proveedor.Email },
                    detalleCompras = c.DetalleCompras.Select(d => new
                    {
                        d.ProductoId,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal,
                        producto = new { nombre = d.Producto!.Nombre } // objeto con nombre
                    })
                })
                .FirstOrDefaultAsync();

            if (compra == null) return NotFound();

            return Ok(compra);
        }

        // POST: api/admin/compras (crear compra + manejar pago)
        [HttpPost]
        public async Task<IActionResult> CreateCompra([FromBody] Compra compra)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                compra.Fecha = DateTime.Now;

                // Agrupar detalles
                compra.DetalleCompras = compra.DetalleCompras
                    .GroupBy(d => d.ProductoId)
                    .Select(g => new DetalleCompra
                    {
                        ProductoId = g.Key,
                        Cantidad = g.Sum(x => x.Cantidad),
                        PrecioUnitario = g.First().PrecioUnitario,
                        Subtotal = g.Sum(x => x.Subtotal)
                    })
                    .ToList();

                compra.Total = compra.DetalleCompras.Sum(d => d.Subtotal);
                compra.Estado = "Pendiente";
                compra.TotalPagado = 0;

                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                // Actualizar stock
                foreach (var detalle in compra.DetalleCompras)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                    if (producto != null)
                    {
                        producto.Stock += detalle.Cantidad;
                        _context.Update(producto);
                    }
                }
                await _context.SaveChangesAsync();

                if (compra.MetodoPagoId == 1) // Efectivo
                {
                    compra.Estado = "Pagada";
                    compra.TotalPagado = compra.Total;
                    compra.FechaPago = DateTime.Now;
                    await _context.SaveChangesAsync();

                    // ✅ Notificar proveedor con el mensaje unificado
                    if (!string.IsNullOrEmpty(compra.Proveedor?.Email))
                    {
                        await _emailService.SendAsync(
                            compra.Proveedor.Email,
                            $"Pago recibido - Compra #{compra.CompraId}",
                            $"Hola {compra.Proveedor.Nombre}, se recibió un pago de {compra.Total:C}. La compra ha sido liquidada en su totalidad."
                        );
                    }

                    await transaction.CommitAsync();
                    return CreatedAtAction(nameof(GetCompra), new { id = compra.CompraId }, compra);
                }

                else // Tarjeta
                {
                    // Para compra nueva, el pago es del total
                    var montoPago = compra.Total; // ⚡ total de la compra
                    var service = new PaymentIntentService();
                    var paymentIntent = await service.CreateAsync(new PaymentIntentCreateOptions
                    {
                        Amount = (long)(montoPago * 100),
                        Currency = "mxn",
                        Description = $"Compra {compra.CompraId}"
                    });

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        compraId = compra.CompraId,
                        clientSecret = paymentIntent.ClientSecret,
                        total = montoPago
                    });
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { error = ex.Message });
            }
        }



        // PUT: api/admin/compras/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCompra(int id, [FromBody] Compra compra)
        {
            if (id != compra.CompraId)
                return BadRequest("El ID de la compra no coincide.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var compraOriginal = await _context.Compras
                    .Include(c => c.DetalleCompras)
                    .Include(c => c.Proveedor)
                    .FirstOrDefaultAsync(c => c.CompraId == id);

                if (compraOriginal == null)
                    return NotFound();

                // 1️⃣ Revertir stock de la compra anterior
                foreach (var detalle in compraOriginal.DetalleCompras)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                    if (producto != null)
                    {
                        producto.Stock -= detalle.Cantidad;
                        if (producto.Stock < 0) producto.Stock = 0;
                    }
                }
                await _context.SaveChangesAsync();

                // Guardar TotalPagado existente y método de pago
                var totalPagadoAnterior = compraOriginal.TotalPagado ?? 0;
                var metodoPagoAnterior = compraOriginal.MetodoPagoId;

                // 2️⃣ Actualizar cabecera
                compraOriginal.Fecha = compra.Fecha;
                compraOriginal.ProveedorId = compra.ProveedorId;
                compraOriginal.MetodoPagoId = compra.MetodoPagoId;

                // 3️⃣ Reemplazar detalles de compra
                _context.DetalleCompras.RemoveRange(compraOriginal.DetalleCompras);
                await _context.SaveChangesAsync();

                var nuevosDetalles = compra.DetalleCompras.Select(d => new DetalleCompra
                {
                    ProductoId = d.ProductoId,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal,
                    CompraId = compraOriginal.CompraId
                }).ToList();

                await _context.DetalleCompras.AddRangeAsync(nuevosDetalles);

                // 4️⃣ Actualizar stock con nuevos detalles
                foreach (var detalle in nuevosDetalles)
                {
                    var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                    if (producto != null)
                        producto.Stock += detalle.Cantidad;
                }

                // 5️⃣ Recalcular total y estado
                compraOriginal.Total = nuevosDetalles.Sum(d => d.Subtotal);
                compraOriginal.TotalPagado = totalPagadoAnterior;

                // Verificar si hay saldo pendiente después de la edición
                var saldoPendiente = compraOriginal.Total - totalPagadoAnterior;
                compraOriginal.Estado = saldoPendiente <= 0 ? "Pagada" : "Pendiente";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 6️⃣ Retornar información del saldo pendiente
                return Ok(new
                {
                    compraId = compraOriginal.CompraId,
                    total = compraOriginal.Total,
                    totalPagado = compraOriginal.TotalPagado,
                    saldoPendiente = saldoPendiente,
                    requierePago = saldoPendiente > 0 && compraOriginal.MetodoPagoId != 1 // No efectivo
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { error = ex.Message });
            }
        }

        // DELETE: api/admin/compras/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")] // 🔹 Solo administradores pueden eliminar
        public async Task<IActionResult> DeleteCompra(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.DetalleCompras)
                .FirstOrDefaultAsync(c => c.CompraId == id);

            if (compra == null) return NotFound();

            foreach (var detalle in compra.DetalleCompras)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null)
                    producto.Stock -= detalle.Cantidad;
            }

            _context.Compras.Remove(compra);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/admin/compras/metodospago
        [HttpGet("metodospago")]
        public async Task<ActionResult<IEnumerable<MetodoPago>>> GetMetodosPago()
        {
            return Ok(await _context.MetodosPago.ToListAsync());
        }

        // GET: api/admin/compras/pendientes
        [HttpGet("pendientes")]
        public async Task<IActionResult> GetComprasPendientes()
        {
            var compras = await _context.Compras
                .Where(c => c.Estado != "Pagada")
                .Select(c => new
                {
                    compraId = c.CompraId,
                    proveedor = new
                    {
                        nombre = c.Proveedor != null ? c.Proveedor.Nombre : string.Empty,
                        email = c.Proveedor != null ? c.Proveedor.Email : null
                    },
                    total = c.Total,
                    totalPagado = c.TotalPagado ?? 0,
                    estado = c.Estado
                })
                .ToListAsync();

            return Ok(compras);
        }
        // POST: api/admin/compras/confirmar-pago
        [HttpPost("confirmar-pago")]
        public async Task<IActionResult> ConfirmarPago([FromBody] PagoCompraDto model)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .FirstOrDefaultAsync(c => c.CompraId == model.CompraId);

            if (compra == null)
                return BadRequest(new { error = "Compra no encontrada" });

            // Registrar el abono
            compra.TotalPagado = (compra.TotalPagado ?? 0) + model.MontoPagado;
            compra.MetodoPagoId = model.MetodoPagoId;
            compra.FechaPago = DateTime.Now;
            compra.ReferenciaPago = model.Referencia;

            // Actualizar estado según pagos
            compra.Estado = compra.TotalPagado >= compra.Total ? "Pagada" : "Pendiente";

            _context.Update(compra);
            await _context.SaveChangesAsync();

            // Calcular saldo pendiente
            var saldoPendiente = compra.Total - compra.TotalPagado.Value;

            // Notificar proveedor
            if (!string.IsNullOrEmpty(compra.Proveedor?.Email))
            {
                string asunto = $"Pago confirmado - Compra #{compra.CompraId}";
                string mensaje = $"Hola {compra.Proveedor.Nombre}, se ha completado el pago de {model.MontoPagado:C}.";

                if (saldoPendiente <= 0)
                {
                    mensaje += " La compra ha sido liquidada en su totalidad.";
                }
                else
                {
                    mensaje += $" Saldo restante: {saldoPendiente:C}.";
                }


                await _emailService.SendAsync(compra.Proveedor.Email, asunto, mensaje);
            }

            return Ok(new { success = true, compra, saldoPendiente });
        }

        // POST: api/admin/compras/generar-payment-intent/{id}
        [HttpPost("generar-payment-intent/{id}")]
        public async Task<IActionResult> GenerarPaymentIntent(int id)
        {
            var compra = await _context.Compras.FindAsync(id);
            if (compra == null) return NotFound();

            var saldoPendiente = (compra.Total - (compra.TotalPagado ?? 0)) * 100; // en centavos
            if (saldoPendiente <= 0) return BadRequest(new { error = "No hay saldo pendiente." });

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = (long)saldoPendiente,
                Currency = "mxn",
                Description = $"Saldo pendiente compra {compra.CompraId}"
            });

            return Ok(new { clientSecret = paymentIntent.ClientSecret, saldoPendiente = saldoPendiente / 100m });
        }



        // POST: api/admin/compras/webhook (Stripe webhook)
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            Event stripeEvent;
            try
            {
                stripeEvent = string.IsNullOrEmpty(webhookSecret)
                    ? EventUtility.ParseEvent(json)
                    : EventUtility.ConstructEvent(
                        json,
                        Request.Headers["Stripe-Signature"],
                        webhookSecret,
                        throwOnApiVersionMismatch: false
                    );
            }
            catch (StripeException e)
            {
                return BadRequest(new { error = e.Message });
            }

            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent != null && int.TryParse(paymentIntent.Description?.Split(' ').Last(), out int compraId))
                {
                    var compra = await _context.Compras.Include(c => c.Proveedor)
                                                       .FirstOrDefaultAsync(c => c.CompraId == compraId);

                    if (compra != null)
                    {
                        // 1️⃣ Obtener el monto pagado desde Stripe en pesos
                        var montoPagado = paymentIntent.AmountReceived / 100m;

                        // 2️⃣ Sumar al total pagado existente
                        compra.TotalPagado = (compra.TotalPagado ?? 0) + montoPagado;

                        // 3️⃣ Actualizar estado
                        compra.Estado = compra.TotalPagado >= compra.Total ? "Pagada" : "Pendiente";
                        compra.FechaPago = DateTime.Now;
                        compra.ReferenciaPago = paymentIntent.Id;

                        _context.Update(compra);
                        await _context.SaveChangesAsync();

                        // 4️⃣ Notificar al proveedor solo si se completó el pago total
                        // ✅ Notificar al proveedor solo si ya quedó liquidada
                        if (compra.Estado == "Pagada" && !string.IsNullOrEmpty(compra.Proveedor?.Email))
                        {
                            await _emailService.SendAsync(
                                compra.Proveedor.Email,
                                $"Pago recibido - Compra #{compra.CompraId}",
                                $"Hola {compra.Proveedor.Nombre}, se recibió un pago de {montoPagado:C}. La compra ha sido liquidada en su totalidad."
                            );
                        }

                    }
                }
            }

            return Ok();
        }
    }
}



