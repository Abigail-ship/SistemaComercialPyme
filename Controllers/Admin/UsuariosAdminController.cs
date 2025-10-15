using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;

namespace SistemaComercialPyme.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public UsuariosController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/admin/usuarios
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Usuario>>> GetUsuarios()
        {
            var usuarios = await _context.Usuarios
                .Include(u => u.Rol)
                .ToListAsync();
            return Ok(usuarios);
        }

        // GET: api/admin/usuarios/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Usuario>> GetUsuario(int id)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.UsuarioId == id);

            if (usuario == null) return NotFound();

            return Ok(usuario);
        }
        // POST: api/admin/usuarios
        [HttpPost]
        public async Task<ActionResult<Usuario>> CreateUsuario([FromBody] Usuario usuario)
        {
            // Validación manual en lugar de ModelState.IsValid
            if (string.IsNullOrEmpty(usuario.NombreUsuario) ||
                string.IsNullOrEmpty(usuario.NombreCompleto) ||
                usuario.RolId <= 0)
            {
                return BadRequest("Datos requeridos incompletos");
            }

            // Validar que la contraseña no sea nula en creación
            if (string.IsNullOrWhiteSpace(usuario.PasswordHash))
            {
                return BadRequest("La contraseña es requerida");
            }

            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(usuario.PasswordHash);
            usuario.Activo = usuario.Activo ?? true; // Valor por defecto

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUsuario), new { id = usuario.UsuarioId }, usuario);
        }
        // PUT: api/admin/usuarios/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUsuario(int id, [FromBody] Usuario usuario)
        {
            if (id != usuario.UsuarioId) return BadRequest();

            var usuarioOriginal = await _context.Usuarios.FindAsync(id);
            if (usuarioOriginal == null) return NotFound();

            // Actualizar solo las propiedades necesarias, NO toda la entidad
            usuarioOriginal.NombreUsuario = usuario.NombreUsuario;
            usuarioOriginal.NombreCompleto = usuario.NombreCompleto;
            usuarioOriginal.RolId = usuario.RolId;
            usuarioOriginal.Activo = usuario.Activo;

            // Mantener la contraseña original si no se envía nueva
            if (!string.IsNullOrWhiteSpace(usuario.PasswordHash) &&
                usuario.PasswordHash != usuarioOriginal.PasswordHash) // Evitar re-hashear si es el mismo hash
            {
                usuarioOriginal.PasswordHash = BCrypt.Net.BCrypt.HashPassword(usuario.PasswordHash);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Usuarios.AnyAsync(e => e.UsuarioId == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/admin/usuarios/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUsuario(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
