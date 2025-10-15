using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using System.Globalization;

namespace SistemaComercialPyme.Controllers.Admin.Reportes
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportesComprasController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ReportesComprasController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // 📌 GET: api/ReportesCompras?tipo=mensual&year=2025&month=9
        [HttpGet]
        public async Task<IActionResult> GetComprasReporte(
            [FromQuery] string tipo = "mensual",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] int? week = null)
        {
            // Log para depuración
            Console.WriteLine($"Parametros recibidos: tipo={tipo}, year={year}, month={month}, week={week}");

            var query = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.DetalleCompras)
                .AsQueryable();

            // Filtrar por año si se proporciona
            if (year.HasValue)
                query = query.Where(c => c.Fecha.HasValue && c.Fecha.Value.Year == year.Value);

            List<Compra> comprasList;

            // 📌 Filtro mensual
            if (tipo == "mensual" && month.HasValue && year.HasValue)
            {
                var startOfMonth = new DateTime(year.Value, month.Value, 1);
                var startOfNextMonth = startOfMonth.AddMonths(1);

                comprasList = await query
                    .Where(c => c.Fecha >= startOfMonth && c.Fecha < startOfNextMonth)
                    .ToListAsync();
            }
            // 📌 Filtro semanal
            else if (tipo == "semanal" && week.HasValue && year.HasValue)
            {
                var startOfWeek = FirstDateOfWeekISO8601(year.Value, week.Value);
                var endOfWeekExclusive = startOfWeek.AddDays(7);

                comprasList = await query
                    .Where(c => c.Fecha >= startOfWeek && c.Fecha < endOfWeekExclusive)
                    .ToListAsync();
            }
            // 📌 Si no hay filtros, traer todo
            else
            {
                comprasList = await query.ToListAsync();
            }

            // Resultado final
            var resultado = new
            {
                Tipo = tipo,
                Año = year,
                Mes = month,
                Semana = week,
                TotalCompras = comprasList.Count,
                TotalGastado = comprasList.Sum(c => c.Total),
                Compras = comprasList.Select(c => new
                {
                    c.CompraId,
                    Proveedor = c.Proveedor != null ? c.Proveedor.Nombre : "",
                    c.Fecha,
                    c.Total,
                    Estado = c.Estado,
                    Productos = c.DetalleCompras.Select(d => new
                    {
                        d.ProductoId,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Subtotal
                    })
                })
            };

            return Ok(resultado);
        }

        // Función auxiliar para calcular el lunes de la semana ISO 8601
        private static DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
        {
            DateTime jan1 = new DateTime(year, 1, 1);
            int daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

            DateTime firstThursday = jan1.AddDays(daysOffset);
            var cal = CultureInfo.InvariantCulture.Calendar;
            int firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

            int weekNum = weekOfYear;
            if (firstWeek <= 1)
                weekNum -= 1;

            DateTime result = firstThursday.AddDays(weekNum * 7);
            return result.AddDays(-3); // lunes de la semana
        }
    }
}
