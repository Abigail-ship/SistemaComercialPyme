using System.ComponentModel.DataAnnotations;

namespace SistemaComercialPyme.Models
{
    public class LoginViewModel
    {
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contraseña { get; set; } = string.Empty;
        public bool Recordarme { get; set; }
    }
}
