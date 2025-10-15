using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BCrypt.Net;
using SistemaComercialPyme.Models;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;



namespace SistemaComercialPyme.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly IConfiguration _config;

        public AuthController(PymeArtesaniasContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.NombreUsuario) || string.IsNullOrWhiteSpace(model.Contraseña))
                return BadRequest("Usuario o contraseña vacíos");

            var usuario = await _context.Usuarios
                .Include(u => u.Rol) // Incluye el rol
                .FirstOrDefaultAsync(u => u.NombreUsuario == model.NombreUsuario);

            if (usuario == null)
                return Unauthorized("Usuario o contraseña incorrectos");

            // Compara la contraseña con hash de la base de datos
            bool isValid = BCrypt.Net.BCrypt.Verify(model.Contraseña, usuario.PasswordHash ?? "");

            if (!isValid)
                return Unauthorized("Usuario o contraseña incorrectos");

            // Claims para JWT
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, usuario.NombreUsuario),
        new Claim(ClaimTypes.Role, usuario.Rol?.Nombre ?? "Usuario"),
        new Claim("NombreCompleto", usuario.NombreCompleto ?? "SinNombre")
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                role = usuario.Rol,
                nombreCompleto = usuario.NombreCompleto ?? usuario.NombreUsuario
            });
        }

    }
}
