using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaComercialPyme.Models;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Services;



namespace SistemaComercialPyme.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PagosController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly StripeService _stripeService;
        private readonly IConfiguration _configuration;

        public PagosController(PymeArtesaniasContext context, StripeService stripeService, IConfiguration configuration)
        {
            _context = context;
            _stripeService = stripeService;
            _configuration = configuration;
        }

        [HttpPost("procesar")]
        public async Task<IActionResult> ProcesarPago([FromBody] PagoRequest request)
        {
            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.Cliente)
                    .FirstOrDefaultAsync(v => v.VentaId == request.VentaId);

                if (venta == null)
                    return NotFound(new { error = "Venta no encontrada" });

                var metodoPago = await _context.MetodosPago.FindAsync(request.MetodoPagoId);
                if (metodoPago == null)
                    return BadRequest(new { error = "Método de pago no válido" });

                decimal pagado = venta.TotalPagado ?? 0;
                decimal saldoPendiente = venta.Total - pagado;

                if (saldoPendiente <= 0)
                    return BadRequest(new { error = "La venta ya está completamente pagada" });

                if (metodoPago.Nombre.Contains("Tarjeta"))
                {
                    var paymentIntent = await _stripeService.CreatePaymentIntentAsync(saldoPendiente, "mxn");

                    venta.StripePaymentIntentId = paymentIntent.Id;
                    venta.MetodoPagoId = request.MetodoPagoId;

                    _context.Update(venta);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        clientSecret = paymentIntent.ClientSecret,
                        stripePublicKey = _configuration["Stripe:PublicKey"]
                    });
                }
                else
                {
                    venta.TotalPagado = pagado + saldoPendiente;
                    venta.MetodoPagoId = request.MetodoPagoId;
                    venta.FechaPago = DateTime.Now;
                    venta.ReferenciaPago = request.Referencia;

                    if (venta.TotalPagado >= venta.Total)
                        venta.Pagado = true;

                    _context.Update(venta);
                    await _context.SaveChangesAsync();

                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpGet("ObtenerClavePublica")]
        public IActionResult ObtenerClavePublica()
        {
            var publicKey = _configuration["Stripe:PublicKey"];
            return Ok(new { publicKey });
        }

        public class ConfirmarPagoRequest
        {
            public int VentaId { get; set; }
        }
        [HttpGet("metodospago")]
        public async Task<IActionResult> GetMetodosPago()
        {
            var metodos = await _context.MetodosPago
                     .Where(m => m.Activo.HasValue && m.Activo.Value != 0)
                     .ToListAsync();

            return Ok(metodos);
        }

        [HttpPost("confirmar")]
        public async Task<IActionResult> ConfirmarPago([FromBody] ConfirmarPagoRequest request)
        {
            try
            {
                var venta = await _context.Ventas.FindAsync(request.VentaId);
                if (venta == null)
                    return NotFound(new { error = "Venta no encontrada" });

                var confirmada = await _stripeService.ConfirmPaymentAsync(request.VentaId);
                if (!confirmada)
                    return BadRequest(new { error = "Error al confirmar el pago con Stripe" });

                decimal pagado = venta.TotalPagado ?? 0;
                decimal saldoPendiente = venta.Total - pagado;

                venta.TotalPagado = pagado + saldoPendiente;

                if (venta.TotalPagado >= venta.Total)
                {
                    venta.Pagado = true;
                    venta.FechaPago = DateTime.Now;
                }

                _context.Update(venta);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
