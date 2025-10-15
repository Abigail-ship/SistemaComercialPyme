using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;

namespace SistemaComercialPyme.Controllers.Admin.Reportes
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportesInventarioController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ReportesInventarioController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        [HttpGet("estado-inventario")]
        public async Task<ActionResult<IEnumerable<ReporteInventarioDto>>> GetEstadoInventario()
        {
            var productos = await _context.Productos
                .Select(p => new ReporteInventarioDto
                {
                    ProductoId = p.ProductoId,
                    Nombre = p.Nombre,
                    Stock = p.Stock,
                    StockMinimo = p.StockMinimo ?? 0,
                    Estado = p.Stock == 0 ? "Agotado" :
                             (p.Stock <= (p.StockMinimo ?? 0) ? "Por agotarse" : "Disponible")
                })
                .ToListAsync();

            return Ok(productos);
        }
    }
}
