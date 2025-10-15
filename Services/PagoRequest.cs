namespace SistemaComercialPyme.Services
{
    public class PagoRequest
    {
        public int VentaId { get; set; }
        public int? MetodoPagoId { get; set; }
        public string? Referencia { get; set; }
    }
}
