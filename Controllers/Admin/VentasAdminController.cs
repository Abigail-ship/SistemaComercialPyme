using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Controllers.Admin.Hubs;
using SistemaComercialPyme.Models;
using SistemaComercialPyme.Services;
using Stripe;
using Stripe.Checkout;

namespace SistemaComercialPyme.Controllers.Admin
{
    [Route("api/[controller]")]
    [ApiController]
    public class VentasAdminController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly StripeService _stripeService;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;
        private readonly IHubContext<NotificacionesHub> _hubContext;

        public VentasAdminController(
            PymeArtesaniasContext context,
            StripeService stripeService,
            IConfiguration config,
            EmailService emailService,
            IHubContext<NotificacionesHub> hubContext)
        {
            _context = context;
            _stripeService = stripeService;
            _config = config;
            _emailService = emailService;
            _hubContext = hubContext;
        }

        // GET: api/Ventas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetVentas()
        {
            var ventas = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.MetodoPago)
                .OrderByDescending(v => v.Fecha)
                .Select(v => new
                {
                    v.VentaId,
                    Cliente = new { v.Cliente!.ClienteId, v.Cliente.Nombres, v.Cliente.Apellidos },
                    MetodoPago = new { v.MetodoPagoId, v.MetodoPago!.Nombre },
                    v.Total,
                    v.TotalPagado,
                    Pagado = v.Pagado
                })
                .ToListAsync();

            return Ok(ventas);
        }


        // GET: api/Ventas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Venta>> GetVenta(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Detalleventa).ThenInclude(d => d.Producto)
                .Include(v => v.MetodoPago)
                .Include(v => v.Transaccionesstripes)
                .FirstOrDefaultAsync(v => v.VentaId == id);

            if (venta == null) return NotFound();

            // Ajustar TotalPagado en caso de ventas ya liquidadas
            venta.TotalPagado = venta.Pagado == true ? venta.Total : (venta.TotalPagado ?? 0);

            return venta;
        }

        // POST: api/Ventas
        [HttpPost]
        public async Task<ActionResult<Venta>> CreateVenta([FromBody] Venta venta)
        {
            if (venta.ClienteId == 0)
                return BadRequest("Debe seleccionar un cliente");

            if (venta.Detalleventa == null || !venta.Detalleventa.Any())
                return BadRequest("Debe agregar al menos un producto");

            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            DateTime fechaMexico = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            venta.Fecha = fechaMexico;
            venta.Total = 0;

            foreach (var detalle in venta.Detalleventa)
            {
                detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;
                venta.Total += detalle.Subtotal;

                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto == null || producto.Stock < detalle.Cantidad)
                    return BadRequest($"Producto no válido o stock insuficiente: {detalle.ProductoId}");

                producto.Stock -= detalle.Cantidad;
            }

            venta.TotalPagado ??= 0;

            if (venta.MetodoPagoId == 1) // Efectivo
            {
                venta.Pagado = true;
                venta.FechaPago = fechaMexico;
                venta.ReferenciaPago = $"EF-{Guid.NewGuid():N}".Substring(0, 8);
                venta.TotalPagado = venta.Total;
            }
            else if (venta.MetodoPagoId == 2 || venta.MetodoPagoId == 3) // Tarjeta/transferencia
            {
                venta.Pagado = false;
                venta.FechaPago = null;
                venta.ReferenciaPago = null;
            }
            else
            {
                return BadRequest("Método de pago no válido.");
            }

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            await _context.Entry(venta).Collection(v => v.Detalleventa).LoadAsync();
            await _context.Entry(venta).Reference(v => v.Cliente).LoadAsync();

            if (venta.MetodoPagoId == 1) // Notificar pago inmediato
            {
                await _hubContext.Clients.All.SendAsync("VentaPagada", new
                {
                    VentaId = venta.VentaId,
                    Cliente = venta.Cliente?.Nombres,
                    Monto = venta.Total,
                    Fecha = venta.FechaPago
                });
            }

            return CreatedAtAction(nameof(GetVenta), new { id = venta.VentaId }, venta);
        }

        // POST: api/Ventas/crear-checkout-session
        [HttpPost("crear-checkout-session")]
        public async Task<IActionResult> CrearCheckoutSession([FromBody] CrearCheckoutRequest request)
        {
            var venta = await _context.Ventas.Include(v => v.Cliente)
    .FirstOrDefaultAsync(v => v.VentaId == request.VentaId);


            if (venta == null) return NotFound("Venta no encontrada.");

            var montoPendiente = venta.Total - (venta.TotalPagado ?? 0);
            if (montoPendiente <= 0)
                return BadRequest("No hay saldo pendiente para pagar.");

            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                Mode = "payment",
                CustomerEmail = venta.Cliente?.Email,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "mxn",
                            UnitAmount = (long)(montoPendiente * 100),
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Pago pendiente Venta #{venta.VentaId}"
                            }
                        },
                        Quantity = 1
                    }
                },
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    Description = $"Pago pendiente VentaId:{venta.VentaId}"
                },
                SuccessUrl = $"http://localhost:4200/pago-exitoso?ventaId={venta.VentaId}",
                CancelUrl = $"http://localhost:4200/pago-cancelado?ventaId={venta.VentaId}",
                Metadata = new Dictionary<string, string>
                {
                    { "VentaId", venta.VentaId.ToString() }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            venta.StripeSessionId = session.Id;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(venta.Cliente?.Email))
            {
                string cuerpo = $@"
                Hola {venta.Cliente.Nombres},<br>
                Gracias por tu compra. Para completar el pago, haz clic en:<br>
                <a href='{session.Url}'>Pagar ahora</a>";
                await _emailService.EnviarCorreoAsync(venta.Cliente.Email, "Link de pago para tu compra", cuerpo);
            }

            return Ok(new { url = session.Url });
        }

        // Webhook Stripe
        [HttpPost("webhook")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"];

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    _config["Stripe:WebhookSecret"],
                    throwOnApiVersionMismatch: false
                );

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    var session = stripeEvent.Data.Object as Session;
                    if (session == null) return Ok();

                    if (session.Metadata == null ||
                        !session.Metadata.TryGetValue("VentaId", out var ventaIdStr) ||
                        !int.TryParse(ventaIdStr, out int ventaId))
                        return Ok();

                    var venta = await _context.Ventas.Include(v => v.Cliente)
                        .FirstOrDefaultAsync(v => v.VentaId == ventaId);

                    if (venta == null) return BadRequest();
                    // 🔹 Zona horaria de México
                    TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
                    DateTime fechaMexico = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);



                    // 🔹 Monto pagado en esta transacción
                    var montoPagado = (session.AmountTotal ?? 0) / 100m;

                    venta.TotalPagado = (venta.TotalPagado ?? 0) + montoPagado;
                    venta.Pagado = venta.TotalPagado >= venta.Total;
                    venta.FechaPago = fechaMexico;
                    venta.StripePaymentIntentId = session.PaymentIntentId;
                    venta.ReferenciaPago = $"ST-{session.PaymentIntentId}";

                    await _context.SaveChangesAsync();

                    await _hubContext.Clients.All.SendAsync("VentaPagada", new
                    {
                        VentaId = venta.VentaId,
                        Cliente = venta.Cliente?.Nombres,
                        Monto = montoPagado,
                        Fecha = venta.FechaPago
                    });

                    if (!string.IsNullOrEmpty(venta.Cliente?.Email))
                    {
                        string cuerpo = $@"
                        Hola {venta.Cliente.Nombres},<br>
                        Confirmamos tu pago por <strong>{montoPagado:C}</strong>.<br>
                        Saldo restante: <strong>{venta.Total - venta.TotalPagado:C}</strong><br>
                        Número de venta: {venta.VentaId}";
                        await _emailService.EnviarCorreoAsync(venta.Cliente.Email, "Confirmación de pago", cuerpo);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error Webhook: {ex.Message}");
                return BadRequest();
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVenta(int id, [FromBody] Venta venta)
        {
            if (id != venta.VentaId) return BadRequest("ID no coincide");

            var ventaOriginal = await _context.Ventas
                .Include(v => v.Detalleventa)
                .Include(v => v.Cliente)
                .FirstOrDefaultAsync(v => v.VentaId == id);

            if (ventaOriginal == null) return NotFound();

            // 🔹 Restaurar stock de los detalles antiguos
            foreach (var detalleOriginal in ventaOriginal.Detalleventa)
            {
                var producto = await _context.Productos.FindAsync(detalleOriginal.ProductoId);
                if (producto != null) producto.Stock += detalleOriginal.Cantidad;
            }

            // 🔹 Limpiar detalles antiguos
            _context.DetalleVenta.RemoveRange(ventaOriginal.Detalleventa);
            ventaOriginal.Detalleventa = new List<DetalleVenta>();
            ventaOriginal.Total = 0;
            ventaOriginal.ClienteId = venta.ClienteId;

            // 🔹 Agregar detalles nuevos y ajustar stock
            foreach (var detalle in venta.Detalleventa)
            {
                detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;
                ventaOriginal.Total += detalle.Subtotal;

                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto == null || producto.Stock < detalle.Cantidad)
                    return BadRequest($"No hay suficiente stock para el producto {detalle.ProductoId}");
                producto.Stock -= detalle.Cantidad;

                ventaOriginal.Detalleventa.Add(detalle);
            }

            // 🔹 Ajustar TotalPagado y estado de pago
            ventaOriginal.TotalPagado = Math.Min(venta.TotalPagado ?? ventaOriginal.TotalPagado ?? 0, ventaOriginal.Total);
            ventaOriginal.Pagado = ventaOriginal.TotalPagado >= ventaOriginal.Total;

            if (ventaOriginal.Pagado.GetValueOrDefault() && venta.MetodoPagoId == 1) // efectivo
            {
                // En UpdateVenta
                TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
                DateTime fechaMexico = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                ventaOriginal.FechaPago ??= fechaMexico;
                ventaOriginal.ReferenciaPago ??= $"EF-{Guid.NewGuid():N}".Substring(0, 8);
                ventaOriginal.TotalPagado = ventaOriginal.Total;
            }

            ventaOriginal.MetodoPagoId = venta.MetodoPagoId;

            _context.Update(ventaOriginal);
            await _context.SaveChangesAsync();

            if (ventaOriginal.Pagado == true)
            {
                await _hubContext.Clients.All.SendAsync("VentaPagada", new
                {
                    VentaId = ventaOriginal.VentaId,
                    Cliente = ventaOriginal.Cliente?.Nombres,
                    Monto = ventaOriginal.Total,
                    Fecha = ventaOriginal.FechaPago
                });
            }

            return NoContent();
        }



        // DELETE: api/Ventas/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")] // 🔹 Solo usuarios con rol Administrador
        public async Task<IActionResult> DeleteVenta(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalleventa)
                .Include(v => v.Transaccionesstripes)
                .FirstOrDefaultAsync(v => v.VentaId == id);

            if (venta == null) return NotFound();

            // Restaurar stock de productos
            foreach (var detalle in venta.Detalleventa)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null) producto.Stock += detalle.Cantidad;
            }

            // Eliminar transacciones Stripe relacionadas
            _context.TransaccionesStripe.RemoveRange(venta.Transaccionesstripes);

            // Eliminar venta
            _context.Ventas.Remove(venta);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
