using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SistemaComercialPyme.Models;

public partial class Usuario
{
    public int UsuarioId { get; set; }
    [Required]
    [Display(Name = "Nombre de usuario")]
    public string NombreUsuario { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    [Required]
    public string NombreCompleto { get; set; } = null!;
    public int RolId { get; set; }
    public bool? Activo { get; set; }
    public virtual Rol? Rol { get; set; } = null!;
}
