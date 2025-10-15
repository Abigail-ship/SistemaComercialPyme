using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;



namespace SistemaComercialPyme.Controllers.Clientes
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosclientesController(PymeArtesaniasContext context) : ControllerBase
    {
        private readonly PymeArtesaniasContext _context = context;

        // POST: api/UsuarioCliente/registrar
        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] UsuariosCliente nuevo)
        {
            if (string.IsNullOrWhiteSpace(nuevo.Email) || string.IsNullOrWhiteSpace(nuevo.PasswordHash))
                return BadRequest(new { mensaje = "Correo y contraseña son obligatorios" });

            bool existe = await _context.UsuariosClientes.AnyAsync(u => u.Email == nuevo.Email);
            if (existe)
                return Conflict(new { mensaje = "El correo ya está registrado" });

            // Hash de contraseña
            nuevo.PasswordHash = HashPassword(nuevo.PasswordHash);
            nuevo.FechaRegistro = DateTime.UtcNow;

            _context.UsuariosClientes.Add(nuevo);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Usuario registrado correctamente" });
        }

        // POST: api/UsuarioCliente/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UsuariosCliente login)
        {
            var usuario = await _context.UsuariosClientes
                .FirstOrDefaultAsync(u => u.Email == login.Email);

            if (usuario == null || !VerifyPassword(login.PasswordHash, usuario.PasswordHash))
                return Unauthorized(new { mensaje = "Credenciales inválidas" });

            // Aquí podrías generar un JWT, pero por simplicidad devolvemos el usuario
            return Ok(new
            {
                usuario.UsuarioId,
                usuario.Email,
                usuario.Nombres,
                usuario.Apellidos
            });
        }

        // GET: api/UsuarioCliente/{usuarioId}/pedidos
        [HttpGet("{usuarioId}/pedidos")]
        public async Task<IActionResult> ObtenerPedidos(int usuarioId)
        {
            var ventas = await _context.Ventas
                .Include(v => v.Detalleventa)
                    .ThenInclude(dv => dv.Producto)
                .Where(v => v.UsuarioId == usuarioId)
                .OrderByDescending(v => v.Fecha)
                .ToListAsync();

            return Ok(ventas);
        }

        // Métodos auxiliares para manejar contraseñas
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string? inputPassword, string? storedHash)
        {
            if (string.IsNullOrEmpty(inputPassword) || string.IsNullOrEmpty(storedHash))
                return false;

            return HashPassword(inputPassword) == storedHash;
        }
    }
}
