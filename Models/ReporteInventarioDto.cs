namespace SistemaComercialPyme.Models
{
    public class ReporteInventarioDto
    {
        public int ProductoId { get; set; }
        public string ?Nombre { get; set; }
        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}
