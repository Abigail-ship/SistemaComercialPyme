using SistemaComercialPyme.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace SistemaComercialPyme.Services
{
    public class StripeService
    {
        private readonly IConfiguration _configuration;
        private readonly PymeArtesaniasContext _context;

        public StripeService(IConfiguration configuration, PymeArtesaniasContext context)
        {
            _configuration = configuration;
            _context = context;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        public async Task<PaymentIntent> CreatePaymentIntentAsync(decimal amount, string currency = "mxn")
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // Stripe usa centavos
                Currency = currency,
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = new Dictionary<string, string>
            {
                { "integration_check", "accept_a_payment" }
            }
            };

            var service = new PaymentIntentService();
            return await service.CreateAsync(options);
        }

        public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
        {
            var service = new PaymentIntentService();
            return await service.GetAsync(paymentIntentId);
        }

        public async Task<bool> ConfirmPaymentAsync(int ventaId)
        {
            var venta = await _context.Ventas.FindAsync(ventaId);
            if (venta == null || string.IsNullOrEmpty(venta.StripePaymentIntentId))
                return false;

            var paymentIntent = await GetPaymentIntentAsync(venta.StripePaymentIntentId);
            if (paymentIntent.Status != "succeeded")
                return false;

            // Marcar como pagado
            venta.Pagado = true;
            venta.FechaPago = DateTime.Now;
            venta.ReferenciaPago = paymentIntent.Id;
            _context.Ventas.Update(venta);

            // ⚠️ Verificar si ya existe una transacción
            var transaccionExistente = await _context.TransaccionesStripe
                .FirstOrDefaultAsync(t => t.VentaId == ventaId);

            if (transaccionExistente == null)
            {
                var transaccion = new TransaccionStripe
                {
                    VentaId = ventaId,
                    Venta = venta!,
                    PaymentIntentId = paymentIntent.Id,
                    ClientSecret = paymentIntent.ClientSecret,
                    Status = paymentIntent.Status,
                    Monto = paymentIntent.Amount / 100m,
                    FechaActualizacion = DateTime.Now
                };

                _context.TransaccionesStripe.Add(transaccion);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public string GetPublicKey()
        {
            return _configuration["Stripe:PublicKey"]!;
        }
    }
}
