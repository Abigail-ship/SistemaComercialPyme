using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Cliente
{
    public int ClienteId { get; set; }
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string? NombreComercial { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? TipoCliente { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public virtual ICollection<Venta> Venta { get; set; } = new List<Venta>();
}
