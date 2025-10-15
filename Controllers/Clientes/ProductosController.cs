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
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosApiController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ProductosApiController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // GET: api/productosapi
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .OrderBy(p => p.Nombre)
                .ToListAsync();
        }

        // GET: api/productosapi/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);

            if (producto == null)
                return NotFound();

            return producto;
        }

        // POST: api/productosapi
        [HttpPost]
        public async Task<ActionResult<Producto>> PostProducto([FromForm] Producto producto, IFormFile ImagenFile)
        {
            producto.FechaCreacion = DateTime.Now;

            if (ImagenFile != null && ImagenFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "uploads/productos");
                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ImagenFile.FileName);
                var filePath = Path.Combine(uploads, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ImagenFile.CopyToAsync(stream);
                }

                producto.Imagen = "/uploads/productos/" + uniqueFileName;
            }

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducto), new { id = producto.ProductoId }, producto);
        }


        // PUT: api/productosapi/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProducto(int id, [FromForm] Producto producto, IFormFile ImagenFile)
        {
            if (id != producto.ProductoId)
                return BadRequest();

            var original = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.ProductoId == id);
            if (original == null)
                return NotFound();

            producto.FechaCreacion = original.FechaCreacion;

            if (ImagenFile != null && ImagenFile.Length > 0)
            {
                var uploads = Path.Combine(Directory.GetCurrentDirectory(), "/uploads/productos");
                if (!Directory.Exists(uploads))
                    Directory.CreateDirectory(uploads);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ImagenFile.FileName);
                var filePath = Path.Combine(uploads, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await ImagenFile.CopyToAsync(stream);
                }

                producto.Imagen = "/uploads/productos/" + uniqueFileName;
            }
            else
            {
                producto.Imagen = original.Imagen; // mantener la imagen actual si no se sube nueva
            }

            _context.Entry(producto).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Productos.Any(e => e.ProductoId == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }


        // DELETE: api/productosapi/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
