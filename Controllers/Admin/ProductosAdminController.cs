using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using SistemaComercialPyme.Services;

namespace SistemaComercialPyme.Controllers.Admin
{
    [Route("api/admin/[controller]")]
    [ApiController]
    public class ProductosAdminController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly S3Service _s3Service;

        public ProductosAdminController(PymeArtesaniasContext context, IWebHostEnvironment hostEnvironment, S3Service s3Service)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
            _s3Service = s3Service;
        }

        private DateTime FechaMexico()
        {
            TimeZoneInfo mexicoZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); // Hora de México
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, mexicoZone);
        }

        // GET: api/admin/productos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos([FromQuery] string? search)
        {
            var productos = _context.Productos.Include(p => p.Categoria).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                productos = productos.Where(p =>
                    (p.Nombre ?? "").Contains(search) ||
                    (p.Categoria != null && p.Categoria.Nombre.Contains(search)));
            }

            return Ok(await productos.OrderBy(p => p.Nombre).ToListAsync());
        }

        // GET: api/admin/productos/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Producto>> GetProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();
            return Ok(producto);
        }

        // POST: api/admin/productos
        [HttpPost]
        public async Task<ActionResult<Producto>> CrearProducto([FromForm] Producto producto, IFormFile? imagenFile)
        {
            if (imagenFile != null && imagenFile.Length > 0)
            {
                // 🔹 Subimos imagen directamente a S3
                string imageUrl = await _s3Service.UploadFileAsync(imagenFile, "uploads/productos");
                producto.Imagen = imageUrl;
            }

            producto.FechaCreacion = FechaMexico();
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducto), new { id = producto.ProductoId }, producto);
        }

        // PUT: api/admin/productos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> EditarProducto(int id, [FromForm] Producto producto, IFormFile? imagenFile)
        {
            if (id != producto.ProductoId) return BadRequest();

            var productoOriginal = await _context.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.ProductoId == id);
            if (productoOriginal == null) return NotFound();

            // Reemplazar imagen si hay nueva
            if(imagenFile != null && imagenFile.Length > 0)
        {
                string imageUrl = await _s3Service.UploadFileAsync(imagenFile, "uploads/productos");
                producto.Imagen = imageUrl;
            }
            else
            {
                producto.Imagen = productoOriginal.Imagen;
            }

            producto.FechaCreacion = productoOriginal.FechaCreacion;

            _context.Update(producto);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        // DELETE: api/admin/productos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/admin/productos/stock-bajo
        [HttpGet("stock-bajo")]
        public async Task<ActionResult<IEnumerable<Producto>>> ProductosConStockBajo()
        {
            var productos = await _context.Productos
                .Where(p => p.Stock <= 5)
                .ToListAsync();

            return Ok(productos);
        }

    }
}
