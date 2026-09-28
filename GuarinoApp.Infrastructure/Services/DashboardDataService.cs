using GuarinoApp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Infrastructure.Services
    {
    public class DashboardDataService : IDashboardDataService
        {
        private readonly IApplicationDbContext _context;

        public DashboardDataService(IApplicationDbContext context) => _context = context;

        public async Task<Dictionary<string, int>> GetStockByVarietyAsync(DateTime? referenceDate = null)
            {
            var fecha = referenceDate ?? DateTime.Now;
            var ultimaFecha = await _context.StocksVariedad
                .Where(x => x.Fecha <= fecha)
                .OrderByDescending(x => x.Fecha)
                .Select(x => (DateTime?)x.Fecha)
                .FirstOrDefaultAsync();

            if (!ultimaFecha.HasValue)
                return new Dictionary<string, int>();

            return await _context.StocksVariedad
                .Where(x => x.Fecha.Year == ultimaFecha.Value.Year && x.Fecha.Month == ultimaFecha.Value.Month)
                .GroupBy(x => x.Variedad ?? "Sin variedad")
                .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Cantidad));
            }

        public async Task<List<MonthlyVolcado>> GetMonthlyVolcadosAsync(DateTime from, DateTime to)
            {
            var fechaInicio = from.Date;
            var fechaFinExclusiva = to.Date.AddDays(1);
            var data = await _context.VolcadosHistoricos
                .Where(v => v.Fecha >= fechaInicio && v.Fecha < fechaFinExclusiva)
                .GroupBy(v => new { v.Año, v.Mes })
                .Select(g => new { g.Key.Año, g.Key.Mes, Kgs = g.Sum(v => v.KgsVolcados) })
                .OrderBy(x => x.Año).ThenBy(x => x.Mes)
                .ToListAsync();

            return data.Select(x => new MonthlyVolcado
                {
                Month = new DateTime(x.Año, x.Mes, 1).ToString("MMM yy"),
                Kgs = (int)x.Kgs
                }).ToList();
            }

        public Task<List<MonthlyVolcado>> GetMonthlyVolcadosAsync(DateTime? referenceDate = null)
            {
            var to = referenceDate ?? DateTime.Today;
            return GetMonthlyVolcadosAsync(to.AddMonths(-11), to);
            }

        public async Task<int> GetTotalStockAsync(DateTime? referenceDate = null)
            => (await GetStockByVarietyAsync(referenceDate)).Values.Sum();

        public async Task<int> GetPalletsWithoutClientAsync(DateTime? referenceDate = null)
            {
            var fechaFinExclusiva = (referenceDate ?? DateTime.Now).Date.AddDays(1);
            return await _context.Pallets
                .Where(p =>
                    !p.IsDeleted &&
                    (p.Cliente == null || p.Cliente.Trim() == string.Empty) &&
                    (p.Fecha ?? p.CreatedAt) < fechaFinExclusiva)
                .CountAsync();
            }

        public async Task<decimal> GetProjectedGrowthAsync(DateTime from, DateTime to)
            {
            var fechaInicio = from.Date;
            var fechaFinExclusiva = to.Date.AddDays(1);
            var duracion = fechaFinExclusiva - fechaInicio;
            var anteriorFinExclusiva = fechaInicio;
            var anteriorInicio = anteriorFinExclusiva - duracion;

            var totalActual = await _context.VolcadosHistoricos
                .Where(v => v.Fecha >= fechaInicio && v.Fecha < fechaFinExclusiva)
                .SumAsync(v => v.KgsVolcados);
            var totalAnterior = await _context.VolcadosHistoricos
                .Where(v => v.Fecha >= anteriorInicio && v.Fecha < anteriorFinExclusiva)
                .SumAsync(v => v.KgsVolcados);

            return totalAnterior == 0 ? 0m : Math.Round(((totalActual - totalAnterior) / totalAnterior) * 100, 1);
            }

        public Task<decimal> GetProjectedGrowthAsync(DateTime? referenceDate = null)
            {
            var to = referenceDate ?? DateTime.Today;
            return GetProjectedGrowthAsync(new DateTime(to.Year, to.Month, 1), to);
            }

        public async Task<List<VolcadoDetalle>> GetVolcadosDetailAsync(DateTime from, DateTime to, int top = 10)
            {
            var fechaInicio = from.Date;
            var fechaFinExclusiva = to.Date.AddDays(1);
            return await _context.VolcadosHistoricos
                .Where(v => v.Fecha >= fechaInicio && v.Fecha < fechaFinExclusiva)
                .GroupBy(v => new
                    {
                    Productor = v.Productor ?? "Desconocido",
                    Variedad = v.Variedad ?? "Sin variedad",
                    Zafra = v.Zafra ?? string.Empty
                    })
                .Select(g => new VolcadoDetalle
                    {
                    Productor = g.Key.Productor,
                    Variedad = g.Key.Variedad,
                    KgsVolcados = g.Sum(v => v.KgsVolcados),
                    Bins = g.Sum(v => v.Bins),
                    Zafra = g.Key.Zafra
                    })
                .OrderByDescending(v => v.KgsVolcados)
                .Take(top)
                .ToListAsync();
            }

        public async Task<DateTime?> GetLastStockDateAsync()
            => await _context.StocksVariedad.OrderByDescending(x => x.Fecha).Select(x => (DateTime?)x.Fecha).FirstOrDefaultAsync();

        public async Task<DateTime?> GetLastStockDateAsync(DateTime referenceDate)
            => await _context.StocksVariedad
                .Where(x => x.Fecha <= referenceDate)
                .OrderByDescending(x => x.Fecha)
                .Select(x => (DateTime?)x.Fecha)
                .FirstOrDefaultAsync();
        }
    }
