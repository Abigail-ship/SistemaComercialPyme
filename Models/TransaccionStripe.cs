using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class TransaccionStripe
{
    public int TransaccionId { get; set; }
    public int VentaId { get; set; }
    public string PaymentIntentId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public decimal Monto { get; set; }
    public string? Moneda { get; set; }
    public virtual Venta Venta { get; set; } = null!;
}
