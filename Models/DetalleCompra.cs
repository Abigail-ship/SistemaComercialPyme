using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class DetalleCompra
{
    public int DetalleId { get; set; }
    public int CompraId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public virtual Compra? Compra { get; set; } 
    public virtual Producto? Producto { get; set; } 
}
