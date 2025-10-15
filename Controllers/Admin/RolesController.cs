using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;

namespace SistemaComercialPyme.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public RolesController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/admin/roles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Rol>>> GetRoles()
        {
            var roles = await _context.Roles.ToListAsync();
            return Ok(roles);
        }

        // GET: api/admin/roles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Rol>> GetRol(int id)
        {
            var rol = await _context.Roles.FindAsync(id);
            if (rol == null) return NotFound();
            return Ok(rol);
        }

        // POST: api/admin/roles
        [HttpPost]
        public async Task<ActionResult<Rol>> CreateRol([FromBody] Rol rol)
        {
            if (await _context.Roles.AnyAsync(r => r.Nombre.ToLower() == rol.Nombre.ToLower()))
                return BadRequest(new { mensaje = "El rol ya existe." });

            _context.Roles.Add(rol);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRol), new { id = rol.RolId }, rol);
        }

        // PUT: api/admin/roles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRol(int id, [FromBody] Rol rol)
        {
            if (id != rol.RolId) return BadRequest();

            _context.Entry(rol).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Roles.AnyAsync(r => r.RolId == rol.RolId))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/admin/roles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRol(int id)
        {
            var rol = await _context.Roles.FindAsync(id);
            if (rol == null) return NotFound();

            _context.Roles.Remove(rol);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
