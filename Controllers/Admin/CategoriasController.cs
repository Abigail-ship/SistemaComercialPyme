using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;

namespace SistemaComercialPyme.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public CategoriasController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/admin/categorias
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Categoria>>> GetCategorias()
        {
            // Incluimos los productos para que Angular pueda verlos
            var categorias = await _context.Categorias
                .Include(c => c.Productos)
                .ToListAsync();
            return Ok(categorias);
        }

        // GET: api/admin/categorias/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Categoria>> GetCategoria(int id)
        {
            var categoria = await _context.Categorias
                .Include(c => c.Productos)
                .FirstOrDefaultAsync(c => c.CategoriaId == id);

            if (categoria == null)
                return NotFound();

            return Ok(categoria);
        }

        // POST: api/admin/categorias
        [HttpPost]
        public async Task<ActionResult<Categoria>> CreateCategoria([FromBody] Categoria categoria)
        {
            if (string.IsNullOrWhiteSpace(categoria.Nombre))
                return BadRequest(new { mensaje = "El nombre de la categoría es obligatorio." });

            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategoria), new { id = categoria.CategoriaId }, categoria);
        }

        // PUT: api/admin/categorias/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategoria(int id, [FromBody] Categoria categoria)
        {
            if (id != categoria.CategoriaId)
                return BadRequest();

            var categoriaExistente = await _context.Categorias.FindAsync(id);
            if (categoriaExistente == null)
                return NotFound();

            categoriaExistente.Nombre = categoria.Nombre;

            _context.Update(categoriaExistente);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/admin/categorias/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategoria(int id)
        {
            var categoria = await _context.Categorias
                .Include(c => c.Productos) // opcional: verificar si tiene productos antes de borrar
                .FirstOrDefaultAsync(c => c.CategoriaId == id);

            if (categoria == null)
                return NotFound();

            if (categoria.Productos.Any())
                return BadRequest(new { mensaje = "No se puede eliminar una categoría que tiene productos." });

            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
