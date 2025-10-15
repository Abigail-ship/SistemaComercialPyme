using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Proveedor
{
    public int ProveedorId { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Rfc { get; set; }
    public bool? Activo { get; set; }
    public virtual ICollection<Compra> Compras { get; set; } = new List<Compra>();
}
