using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Compra
{
    public int CompraId { get; set; }
    public DateTime? Fecha { get; set; }
    public int ProveedorId { get; set; }
    public decimal Total { get; set; }
    public decimal? TotalPagado { get; set; }
    public int? MetodoPagoId { get; set; }
    public string? ReferenciaPago { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? Estado { get; set; }

    public virtual MetodoPago? MetodoPago { get; set; }
    public virtual Proveedor? Proveedor { get; set; } 
    public virtual ICollection<DetalleCompra> DetalleCompras { get; set; } = new List<DetalleCompra>();

}
