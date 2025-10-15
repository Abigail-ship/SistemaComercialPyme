using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Producto
{
    public int ProductoId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public int? CategoriaId { get; set; }
    public decimal Costo { get; set; }
    public decimal PrecioVenta { get; set; }
    public int Stock { get; set; }
    public int? StockMinimo { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public string? Imagen { get; set; }
    public virtual Categoria? Categoria { get; set; }
    public virtual ICollection<DetalleCompra> DetalleCompras { get; set; } = new List<DetalleCompra>();
    public virtual ICollection<DetalleVenta> Detalleventa { get; set; } = new List<DetalleVenta>();
}
