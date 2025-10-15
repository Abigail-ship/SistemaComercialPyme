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
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ProveedoresAdminController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ProveedoresAdminController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/admin/proveedores
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Proveedor>>> GetProveedores([FromQuery] string? searchString)
        {
            var proveedores = _context.Proveedores.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                proveedores = proveedores.Where(p =>
                    (p.Nombre ?? "").Contains(searchString) ||
                    (p.Contacto ?? "").Contains(searchString));
            }

            return Ok(await proveedores.OrderBy(p => p.Nombre).ToListAsync());
        }

        // GET: api/admin/proveedores/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Proveedor>> GetProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return NotFound();

            return Ok(proveedor);
        }
        // POST: api/admin/proveedores
        [HttpPost]
        public async Task<ActionResult<Proveedor>> PostProveedor(Proveedor proveedor)
        {
            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProveedor), new { id = proveedor.ProveedorId }, proveedor);
        }

        // PUT: api/admin/proveedores/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProveedor(int id, Proveedor proveedor)
        {
            if (id != proveedor.ProveedorId)
                return BadRequest();

            _context.Entry(proveedor).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProveedorExists(id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }
        // DELETE: api/admin/proveedores/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProveedor(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null)
                return NotFound();

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProveedorExists(int id)
        {
            return _context.Proveedores.Any(e => e.ProveedorId == id);
        }
    }
}
