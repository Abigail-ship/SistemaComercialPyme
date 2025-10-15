using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;

namespace SistemaComercialPyme.Controllers.Clientes
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController(PymeArtesaniasContext context) : ControllerBase
    {
        private readonly PymeArtesaniasContext _context = context;

        // GET: api/ClientesApi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes([FromQuery] string? searchString)
        {
            var clientes = _context.Clientes.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                clientes = clientes.Where(c =>
                    (c.Nombres ?? "").Contains(searchString) ||
                    (c.Apellidos ?? "").Contains(searchString) ||
                    (c.NombreComercial ?? "").Contains(searchString));
            }

            return Ok(await clientes.OrderBy(c => c.Nombres).ToListAsync());
        }

        // GET: api/ClientesApi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Cliente>> GetCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);

            if (cliente == null)
            {
                return NotFound();
            }

            return Ok(cliente);
        }

        // POST: api/ClientesApi
        [HttpPost]
        public async Task<ActionResult<Cliente>> PostCliente(Cliente cliente)
        {
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCliente), new { id = cliente.ClienteId }, cliente);
        }

        // PUT: api/ClientesApi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCliente(int id, Cliente cliente)
        {
            if (id != cliente.ClienteId)
            {
                return BadRequest();
            }

            _context.Entry(cliente).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClienteExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/ClientesApi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound();
            }

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        // GET: api/ClientesApi/email/{email}
        [HttpGet("email/{email}")]
        public async Task<ActionResult<Cliente>> GetClientePorEmail(string email)
        {
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == email.ToLower());

            if (cliente == null)
                return NotFound();

            return Ok(cliente);
        }


        private bool ClienteExists(int id)
        {
            return _context.Clientes.Any(e => e.ClienteId == id);
        }
    }
}
