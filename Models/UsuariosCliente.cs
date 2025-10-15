using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class UsuariosCliente
{
    public int UsuarioId { get; set; }
    public string? Email { get; set; } = null!;
    public string? Nombres { get; set; } = null!;
    public string? Apellidos { get; set; } = null!;
    public string? PasswordHash { get; set; } = null!;
    public DateTime? FechaRegistro { get; set; }
    public virtual ICollection<Venta> ?Ventas { get; set; } = new List<Venta>();
}
