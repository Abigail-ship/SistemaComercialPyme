using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using SistemaComercialPyme.Services;
using Stripe;
using Stripe.Checkout;


namespace SistemaComercialPyme.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class PagosAdminController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public PagosAdminController(PymeArtesaniasContext context, EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }
        [HttpPost("crear-payment-intent")]
        public async Task<IActionResult> CrearPaymentIntent([FromBody] CrearPaymentIntentDto dto)
        {
            var compra = await _context.Compras.Include(c => c.Proveedor)
                                               .FirstOrDefaultAsync(c => c.CompraId == dto.CompraId);
            if (compra == null) return NotFound();

            // Crear PaymentIntent con descripción para identificar la compra
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(compra.Total * 100), // en centavos
                Currency = "mxn",
                Description = $"Compra {compra.CompraId}"
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            // ⚡ Aquí puedes actualizar la compra como "en proceso de pago" si quieres
            // compra.Estado = "En proceso";
            // await _context.SaveChangesAsync();

            return Ok(new { clientSecret = paymentIntent.ClientSecret });
        }
        [HttpPost("confirmar-pago")]
        public async Task<IActionResult> ConfirmarPago([FromBody] CrearPaymentIntentDto dto)
        {
            var compra = await _context.Compras.Include(c => c.Proveedor)
                                               .FirstOrDefaultAsync(c => c.CompraId == dto.CompraId);
            if (compra == null) return NotFound();

            compra.Estado = "Pagada";
            compra.TotalPagado = compra.Total;
            compra.FechaPago = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Compra marcada como pagada" });
        }

        [HttpGet("pendientes")]
        public async Task<IActionResult> GetComprasPendientes()
        {
            try
            {
                var compras = await _context.Compras
                    .Where(c => c.Estado != "Pagada")
                    .Select(c => new {
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
            catch (Exception ex)
            {
                // ⚡ aquí vas a ver el error real
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }
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
                Console.WriteLine($"❌ Error validando firma: {e.Message}");
                return BadRequest(new { error = e.Message });
            }

            Console.WriteLine($"✅ Evento recibido: {stripeEvent.Type}");

            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                if (paymentIntent != null)
                {
                    Console.WriteLine($"👉 PaymentIntent {paymentIntent.Id} con descripción: {paymentIntent.Description}");

                    if (int.TryParse(paymentIntent.Description?.Split(' ').Last(), out int compraId))
                    {
                        var compra = await _context.Compras.Include(c => c.Proveedor)
                                    .FirstOrDefaultAsync(c => c.CompraId == compraId);

                        if (compra != null)
                        {
                            compra.TotalPagado = compra.Total;
                            compra.Estado = "Pagada";
                            compra.FechaPago = DateTime.Now;
                            compra.ReferenciaPago = paymentIntent.Id;

                            _context.Update(compra);
                            await _context.SaveChangesAsync();

                            Console.WriteLine($"✅ Compra {compra.CompraId} marcada como pagada.");

                            // Enviar correo de forma segura
                            var proveedorEmail = compra.Proveedor?.Email;
                            if (!string.IsNullOrEmpty(proveedorEmail))
                            {
                                await _emailService.SendAsync(
                                    proveedorEmail,
                                    $"Pago recibido - Compra #{compra.CompraId}",
                                    $"Hola {compra.Proveedor?.Nombre}, se recibió el pago de {compra.Total}."
                                );

                                Console.WriteLine($"📧 Intento de envío de correo a {proveedorEmail}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ No se encontró la compra con ID {compraId}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ No se pudo parsear ID de compra desde la descripción: {paymentIntent.Description}");
                    }
                }
            }

            // Retornamos 200 siempre para que Stripe no reintente
            return Ok();
        }

    }
}
