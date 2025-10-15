using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class MetodoPago
{
    public int MetodoPagoId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public sbyte? RequiereReferencia { get; set; }
    public sbyte? Activo { get; set; }
    public virtual ICollection<Compra> Compras { get; set; } = new List<Compra>();
    public virtual ICollection<Venta> Venta { get; set; } = new List<Venta>();
}
