using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using System.Text.RegularExpressions;

namespace SistemaComercialPyme.Controllers.Clientes
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuscripcionesController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public SuscripcionesController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/suscripciones
        [HttpGet]
        public async Task<IActionResult> GetSuscripciones()
        {
            var suscripciones = await _context.Suscripciones
                .OrderByDescending(s => s.FechaRegistro)
                .ToListAsync();

            return Ok(suscripciones);
        }

        // POST: api/suscripciones
        [HttpPost]
        public async Task<IActionResult> AgregarCorreo([FromBody] Suscripcion nueva)
        {
            if (string.IsNullOrWhiteSpace(nueva.Email))
                return BadRequest(new { mensaje = "El correo es obligatorio." });

            // Validar formato de correo
            if (!Regex.IsMatch(nueva.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return BadRequest(new { mensaje = "El formato de correo no es válido." });

            // Verificar si ya existe
            bool existe = await _context.Suscripciones
                .AnyAsync(s => s.Email == nueva.Email);

            if (existe)
                return Conflict(new { mensaje = "El correo ya está suscrito." });

            // Asignar fecha en el backend
            nueva.FechaRegistro = DateTime.UtcNow;

            _context.Suscripciones.Add(nueva);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSuscripciones), new { id = nueva.SuscripcionId }, nueva);
        }
    }
}
