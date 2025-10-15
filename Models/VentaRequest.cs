namespace SistemaComercialPyme.Models
{
    public class VentaRequest
    {
        public int UsuarioId { get; set; }
        public string UsuarioEmail { get; set; } = string.Empty;

        public List<DetalleVentaRequest> DetalleVentas { get; set; } = new();
    }

    public class DetalleVentaRequest
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
    }
}
