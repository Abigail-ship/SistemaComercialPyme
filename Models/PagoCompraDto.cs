namespace SistemaComercialPyme.Models
{
    public class PagoCompraDto
    {
        public int CompraId { get; set; }
        public int MetodoPagoId { get; set; }
        public string? Referencia { get; set; }
        public decimal MontoPagado { get; set; }

    }
}
