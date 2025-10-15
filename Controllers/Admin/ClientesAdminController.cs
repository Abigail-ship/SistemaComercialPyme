using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;

namespace SistemaComercialPyme.Controllers.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ClientesAdminController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ClientesAdminController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/admin/clientes?searchString=algo&page=1&pageSize=10
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes(
            [FromQuery] string? searchString,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var clientes = _context.Clientes.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                clientes = clientes.Where(c =>
                    (c.Nombres ?? "").Contains(searchString) ||
                    (c.Apellidos ?? "").Contains(searchString) ||
                    (c.NombreComercial ?? "").Contains(searchString));
            }

            var total = await clientes.CountAsync();

            var data = await clientes
                .OrderBy(c => c.Nombres)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Data = data
            });
        }
        // GET: api/admin/clientes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null)
                return NotFound();

            return Ok(cliente);
        }

        // POST: api/admin/clientes
        [HttpPost]
        public async Task<ActionResult<Cliente>> PostCliente(Cliente cliente)
        {
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCliente), new { id = cliente.ClienteId }, cliente);
        }

        // PUT: api/admin/clientes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, Cliente cliente)
        {
            if (id != cliente.ClienteId)
                return BadRequest();

            _context.Entry(cliente).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClienteExists(id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }
        // DELETE: api/admin/clientes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ClienteExists(int id)
        {
            return _context.Clientes.Any(e => e.ClienteId == id);
        }
    }

}
