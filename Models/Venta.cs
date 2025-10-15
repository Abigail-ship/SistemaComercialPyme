using System;
using System.Collections.Generic;

namespace SistemaComercialPyme.Models;

public partial class Venta
{
    public int VentaId { get; set; }
    public DateTime? Fecha { get; set; }
    public int ClienteId { get; set; }
    public int? UsuarioId { get; set; }
    public decimal Total { get; set; }
    public decimal? TotalPagado { get; set; }
    public int? MetodoPagoId { get; set; }
    public bool? Pagado { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? ReferenciaPago { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeSessionId { get; set; }
    public string? Estado { get; set; }
    public virtual Cliente? Cliente { get; set; } = null!;
    public virtual ICollection<DetalleVenta> Detalleventa { get; set; } = new List<DetalleVenta>();
    public virtual MetodoPago? MetodoPago { get; set; }
    public virtual ICollection<TransaccionStripe> Transaccionesstripes { get; set; } = new List<TransaccionStripe>();
    public virtual UsuariosCliente? Usuario { get; set; }
}
