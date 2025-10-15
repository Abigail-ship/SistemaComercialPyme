using Microsoft.AspNetCore.SignalR;

namespace SistemaComercialPyme.Controllers.Admin.Hubs
{
    public class NotificacionesHub : Hub
    {
        // Método opcional por si quieres mandar mensajes directos
        public async Task EnviarMensaje(string usuario, string mensaje)
        {
            await Clients.All.SendAsync("RecibirMensaje", usuario, mensaje);
        }

        // Nuevo método para notificar que una venta fue pagada
        public async Task VentaPagada(object data)
        {
            // Envía a todos los clientes conectados el evento "VentaPagada"
            await Clients.All.SendAsync("VentaPagada", data);
        }
    }
}
