using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaComercialPyme.Models;
using System.Globalization;

namespace SistemaComercialPyme.Controllers.Admin.Reportes
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportesVentasController : ControllerBase
    {
        private readonly PymeArtesaniasContext _context;

        public ReportesVentasController(PymeArtesaniasContext context)
        {
            _context = context;
        }

        // 📌 GET: api/ReportesVentas?tipo=mensual&year=2025&month=9
        [HttpGet]
        public async Task<IActionResult> GetVentasReporte(
            [FromQuery] string tipo = "mensual",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] int? week = null)
        {
            Console.WriteLine($"Parametros recibidos: tipo={tipo}, year={year}, month={month}, week={week}");

            var query = _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.MetodoPago)
                .Include(v => v.Detalleventa)
                .AsQueryable();

            if (year.HasValue)
                query = query.Where(v => v.Fecha.HasValue && v.Fecha.Value.Year == year.Value);

            List<Venta> ventasList;

            if (tipo == "mensual" && month.HasValue && year.HasValue)
            {
                var startOfMonth = new DateTime(year.Value, month.Value, 1);
                var startOfNextMonth = startOfMonth.AddMonths(1);

                ventasList = await query
                    .Where(v => v.Fecha >= startOfMonth && v.Fecha < startOfNextMonth)
                    .ToListAsync();
            }
            else if (tipo == "semanal" && week.HasValue && year.HasValue)
            {
                var startOfWeek = FirstDateOfWeekISO8601(year.Value, week.Value);
                var endOfWeekExclusive = startOfWeek.AddDays(7);

                ventasList = await query
                    .Where(v => v.Fecha >= startOfWeek && v.Fecha < endOfWeekExclusive)
                    .ToListAsync();
            }
            else
            {
                ventasList = await query.ToListAsync();
            }

            var resultado = new
            {
                Tipo = tipo,
                Año = year,
                Mes = month,
                Semana = week,
                TotalVentas = ventasList.Count,
                TotalIngresos = ventasList.Sum(v => v.Total),
                Ventas = ventasList.Select(v => new
                {
                    v.VentaId,
                    Cliente = v.Cliente != null ? $"{v.Cliente.Nombres} {v.Cliente.Apellidos}" : "",
                    MetodoPago = v.MetodoPago != null ? v.MetodoPago.Nombre : "",
                    v.Fecha,
                    v.Total,
                    v.TotalPagado,
                    Estado = v.Estado,
                    Pagado = v.Pagado,
                    Productos = v.Detalleventa.Select(d => new
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
            return result.AddDays(-3);
        }
    }
}
