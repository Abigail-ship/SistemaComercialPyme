using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Suscripcion
{
    public int SuscripcionId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime? FechaRegistro { get; set; }
}
