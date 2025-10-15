using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using System.Globalization;

namespace SistemaComercialPyme.Controllers.Admin.Reportes
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportesProductosController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ReportesProductosController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductosMasVendidos(
            [FromQuery] string tipo = "mensual",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] int? week = null)
        {
            Console.WriteLine($"Parametros recibidos: tipo={tipo}, year={year}, month={month}, week={week}");

            var query = _context.DetalleVenta
                .Include(dv => dv.Producto)
                .Include(dv => dv.Venta)
                .AsQueryable();

            if (year.HasValue)
                query = query.Where(dv => dv.Venta!.Fecha.HasValue && dv.Venta.Fecha.Value.Year == year.Value);

            // 📌 Filtro mensual
            if (tipo == "mensual" && month.HasValue && year.HasValue)
            {
                var startOfMonth = new DateTime(year.Value, month.Value, 1);
                var startOfNextMonth = startOfMonth.AddMonths(1);

                query = query.Where(dv => dv.Venta!.Fecha >= startOfMonth && dv.Venta.Fecha < startOfNextMonth);
            }
            // 📌 Filtro semanal
            else if (tipo == "semanal" && week.HasValue && year.HasValue)
            {
                var startOfWeek = FirstDateOfWeekISO8601(year.Value, week.Value);
                var endOfWeek = startOfWeek.AddDays(7);

                query = query.Where(dv => dv.Venta!.Fecha >= startOfWeek && dv.Venta.Fecha < endOfWeek);
            }

            // 📌 Agrupamos productos más vendidos
            var productos = await query
                .GroupBy(dv => new { dv.ProductoId, dv.Producto!.Nombre })
                .Select(g => new
                {
                    ProductoId = g.Key.ProductoId,
                    Nombre = g.Key.Nombre,
                    CantidadVendida = g.Sum(x => x.Cantidad),
                    TotalGenerado = g.Sum(x => x.Subtotal)
                })
                .OrderByDescending(p => p.CantidadVendida)
                .ToListAsync();

            var resultado = new
            {
                Tipo = tipo,
                Año = year,
                Mes = month,
                Semana = week,
                TotalProductos = productos.Count,
                Productos = productos
            };

            return Ok(resultado);
        }

        // 📌 Auxiliar: primer lunes de la semana ISO 8601
        private static DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.InvariantCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            int weekNum = weekOfYear;
            if (firstWeek <= 1) weekNum -= 1;

            DateTime result = firstThursday.AddDays(weekNum * 7);
            return result.AddDays(-3);
        }
    }
}

