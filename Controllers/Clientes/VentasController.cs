using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using SistemaComercialPyme.Services;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaComercialPyme.Controllers.Clientes
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasApiController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly StripeService _stripeService;

        public VentasApiController(PymeArtesaniasContext context, StripeService stripeService)
        {
            _context = context;
            _stripeService = stripeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetVentas([FromQuery] string? searchString, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var ventas = _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Detalleventa)
                    .ThenInclude(d => d.Producto)
                .Include(v => v.MetodoPago)
                .Where(v => v.Cliente != null);

            if (!string.IsNullOrEmpty(searchString))
            {
                ventas = ventas.Where(v =>
                    (v.Cliente!.Nombres + " " + v.Cliente.Apellidos).Contains(searchString));
            }

            var result = await ventas
                .OrderByDescending(v => v.Fecha)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetVenta(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Detalleventa)
                .ThenInclude(d => d.Producto)
                .Include(v => v.MetodoPago)
                .FirstOrDefaultAsync(m => m.VentaId == id);

            if (venta == null)
                return NotFound();

            return Ok(venta);
        }

        [HttpPost]
        public async Task<IActionResult> CreateVenta([FromBody] VentaRequest request)
        {
            if (request.UsuarioId == 0)
                return BadRequest("Debe estar logueado para realizar la compra");

            if (request.DetalleVentas == null || request.DetalleVentas.Count == 0)
                return BadRequest("Debe agregar al menos un producto");

            // 🔹 Definir zona horaria de México
            TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
            DateTime fechaMexico = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);


            // 🔹 Buscar cliente por correo
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == request.UsuarioEmail.ToLower());

            if (cliente == null)
                return BadRequest("El correo del usuario logueado no coincide con ningún cliente registrado. Registre sus datos primero.");

            // 🔹 Buscar venta no pagada del cliente
            var ventaExistente = await _context.Ventas
                .Include(v => v.Detalleventa)
                .FirstOrDefaultAsync(v => v.ClienteId == cliente.ClienteId && v.Pagado == false);

            if (ventaExistente != null)
            {
                var productosActuales = ventaExistente.Detalleventa.ToList();

                // 🔹 Eliminar productos que ya no están en la nueva lista
                foreach (var detalleExistente in productosActuales)
                {
                    if (!request.DetalleVentas.Any(d => d.ProductoId == detalleExistente.ProductoId))
                    {
                        // Revertir stock
                        var producto = await _context.Productos.FindAsync(detalleExistente.ProductoId);
                        if (producto != null)
                            producto.Stock += detalleExistente.Cantidad;

                        _context.DetalleVenta.Remove(detalleExistente);
                    }
                }

                // 🔹 Guardar cambios de eliminación primero
                await _context.SaveChangesAsync();

                // 🔹 Agregar o actualizar productos restantes
                foreach (var detalleReq in request.DetalleVentas)
                {
                    var producto = await _context.Productos.FindAsync(detalleReq.ProductoId);
                    if (producto == null)
                        return BadRequest($"Producto con ID {detalleReq.ProductoId} no existe.");

                    var detalleExistente = await _context.DetalleVenta
                        .FirstOrDefaultAsync(d => d.VentaId == ventaExistente.VentaId && d.ProductoId == detalleReq.ProductoId);

                    if (detalleExistente != null)
                    {
                        int diferencia = detalleReq.Cantidad - detalleExistente.Cantidad;
                        if (diferencia > 0 && producto.Stock < diferencia)
                            return BadRequest($"Stock insuficiente para {producto.Nombre}");

                        // Ajustar stock según diferencia
                        producto.Stock -= diferencia;
                        detalleExistente.Cantidad = detalleReq.Cantidad;
                        detalleExistente.PrecioUnitario = detalleReq.PrecioUnitario;
                        detalleExistente.Subtotal = detalleReq.Cantidad * detalleReq.PrecioUnitario;
                    }
                    else
                    {
                        if (producto.Stock < detalleReq.Cantidad)
                            return BadRequest($"Stock insuficiente para {producto.Nombre}");

                        producto.Stock -= detalleReq.Cantidad;

                        await _context.DetalleVenta.AddAsync(new DetalleVenta
                        {
                            VentaId = ventaExistente.VentaId,
                            ProductoId = detalleReq.ProductoId,
                            Cantidad = detalleReq.Cantidad,
                            PrecioUnitario = detalleReq.PrecioUnitario,
                            Subtotal = detalleReq.Cantidad * detalleReq.PrecioUnitario
                        });
                    }
                }

                // 🔹 Guardar los cambios de detalle
                await _context.SaveChangesAsync();

                // 🔹 Recalcular total real desde la BD
                ventaExistente.Total = await _context.DetalleVenta
                    .Where(d => d.VentaId == ventaExistente.VentaId)
                    .SumAsync(d => d.Subtotal);

                ventaExistente.Fecha = fechaMexico;

                _context.Update(ventaExistente);
                await _context.SaveChangesAsync();

                return Ok(ventaExistente);
            }

            // 🔹 Si no existe venta pendiente, crear una nueva
            var nuevaVenta = new Venta
            {
                UsuarioId = request.UsuarioId,
                ClienteId = cliente.ClienteId,
                Fecha = fechaMexico,
                Pagado = false,
                Detalleventa = new List<DetalleVenta>()
            };

            foreach (var detalleReq in request.DetalleVentas)
            {
                var producto = await _context.Productos.FindAsync(detalleReq.ProductoId);
                if (producto == null || producto.Stock < detalleReq.Cantidad)
                    return BadRequest($"Producto no válido o stock insuficiente para {detalleReq?.ProductoId}");

                producto.Stock -= detalleReq.Cantidad;

                nuevaVenta.Detalleventa.Add(new DetalleVenta
                {
                    ProductoId = detalleReq.ProductoId,
                    Cantidad = detalleReq.Cantidad,
                    PrecioUnitario = detalleReq.PrecioUnitario,
                    Subtotal = detalleReq.Cantidad * detalleReq.PrecioUnitario
                });
            }

            nuevaVenta.Total = nuevaVenta.Detalleventa.Sum(d => d.Subtotal);

            _context.Add(nuevaVenta);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetVenta), new { id = nuevaVenta.VentaId }, nuevaVenta);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVenta(int id, [FromBody] Venta venta)
        {
            if (id != venta.VentaId)
                return BadRequest("El ID no coincide");

            if (venta.ClienteId == 0)
                return BadRequest("Debe seleccionar un cliente");

            if (venta.Detalleventa == null || venta.Detalleventa.Count == 0)
                return BadRequest("Debe agregar al menos un producto");

            var ventaOriginal = await _context.Ventas
                .Include(v => v.Detalleventa)
                .FirstOrDefaultAsync(v => v.VentaId == id);

            if (ventaOriginal == null)
                return NotFound();

            foreach (var detalleOriginal in ventaOriginal.Detalleventa)
            {
                var producto = await _context.Productos.FindAsync(detalleOriginal.ProductoId);
                if (producto != null)
                    producto.Stock += detalleOriginal.Cantidad;
            }

            _context.DetalleVenta.RemoveRange(ventaOriginal.Detalleventa);

            ventaOriginal.ClienteId = venta.ClienteId;
            ventaOriginal.Total = 0;

            foreach (var detalle in venta.Detalleventa)
            {
                detalle.Subtotal = detalle.Cantidad * detalle.PrecioUnitario;
                ventaOriginal.Total += detalle.Subtotal;

                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto == null || producto.Stock < detalle.Cantidad)
                    return BadRequest($"No hay suficiente stock para {producto?.Nombre}");

                producto.Stock -= detalle.Cantidad;
                ventaOriginal.Detalleventa.Add(detalle);
            }

            if ((ventaOriginal.TotalPagado ?? 0) < ventaOriginal.Total)
            {
                ventaOriginal.Pagado = false;
                ventaOriginal.FechaPago = null;
                ventaOriginal.ReferenciaPago = null;
                ventaOriginal.MetodoPagoId = null;
                ventaOriginal.StripePaymentIntentId = null;
            }

            _context.Update(ventaOriginal);
            await _context.SaveChangesAsync();

            return Ok(ventaOriginal);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVenta(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalleventa)
                .FirstOrDefaultAsync(v => v.VentaId == id);

            if (venta == null)
                return NotFound();

            var transaccion = await _context.TransaccionesStripe
                .FirstOrDefaultAsync(t => t.VentaId == venta.VentaId);

            if (transaccion != null)
                _context.TransaccionesStripe.Remove(transaccion);

            foreach (var detalle in venta.Detalleventa)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null)
                    producto.Stock += detalle.Cantidad;
            }

            _context.Ventas.Remove(venta);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("precio/{productoId}")]
        public async Task<IActionResult> GetPrecioProducto(int productoId)
        {
            var producto = await _context.Productos.FindAsync(productoId);
            if (producto == null)
                return NotFound();

            return Ok(new { precio = producto.PrecioVenta });
        }

    }
}
