using System.Data;
using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Infrastructure.Data;

namespace GuarinoApp.Infrastructure.Services;

/// <summary>
/// Implementación real de IDashboardDataService: consulta directamente las tablas
/// sembradas por DbInitializer (Lotes, Volcados, Pallets, Variedades, Productores)
/// usando SQL parametrizado sobre la conexión de EF Core.
///
/// IMPORTANTE: NO se debe hacer `using`/dispose sobre la conexión que devuelve
/// context.Database.GetDbConnection() — esa conexión es propiedad del DbContext
/// (Scoped) y se reutiliza en el resto del request; solo se abre si hace falta.
/// </summary>
public class EfDashboardDataService : IDashboardDataService
{
    private readonly ApplicationDbContext _context;

    public EfDashboardDataService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Stock por variedad "a fin del mes seleccionado": suma los bins de los lotes
    /// ingresados hasta el último día de ese mes (inclusive). Si no se pasa fecha,
    /// usa el estado actual (todos los lotes, sin corte de fecha).
    /// </summary>
    public async Task<Dictionary<string, int>> GetStockByVarietyAsync(DateTime? referenceDate = null)
    {
        var finDeMes = FinDeMes(referenceDate);

        var sql = @"
            SELECT v.Nombre AS Variedad, SUM(l.CantidadBins) AS Bins
            FROM Lotes l
            INNER JOIN Variedades v ON v.IdVariedad = l.IdVariedad
            WHERE (@finDeMes IS NULL OR l.FechaIngreso <= @finDeMes)
            GROUP BY v.Nombre
            ORDER BY Bins DESC";

        var result = new Dictionary<string, int>();

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParam(command, "@finDeMes", (object?)finDeMes ?? DBNull.Value);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var variedad = reader.IsDBNull(0) ? "Sin variedad" : reader.GetString(0);
            var bins = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
            result[variedad] = bins;
        }

        return result;
    }

    /// <summary>
    /// Volcados de los 12 meses que terminan en el mes seleccionado (inclusive).
    /// Si no se pasa fecha, los 12 meses terminan en el mes actual.
    /// </summary>
    public async Task<List<MonthlyVolcado>> GetMonthlyVolcadosAsync(DateTime? referenceDate = null)
    {
        var fin = FinDeMes(referenceDate) ?? FinDeMes(DateTime.Now)!.Value;
        var inicio = fin.AddMonths(-11).AddDays(1 - fin.AddMonths(-11).Day); // primer día, 12 meses atrás

        const string sql = @"
            SELECT
                FORMAT(FechaVolcado, 'yyyy-MM') AS Periodo,
                DATENAME(MONTH, FechaVolcado) AS MesNombre,
                SUM(PesoKilos) AS Kgs
            FROM Volcados
            WHERE FechaVolcado >= @inicio AND FechaVolcado <= @fin
            GROUP BY FORMAT(FechaVolcado, 'yyyy-MM'), DATENAME(MONTH, FechaVolcado)
            ORDER BY Periodo ASC";

        var result = new List<MonthlyVolcado>();

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParam(command, "@inicio", inicio);
        AddParam(command, "@fin", fin);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new MonthlyVolcado
            {
                Month = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1),
                Kgs = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))
            });
        }

        return result;
    }

    public async Task<int> GetTotalStockAsync(DateTime? referenceDate = null)
    {
        var finDeMes = FinDeMes(referenceDate);
        return await ExecuteScalarIntAsync(
            "SELECT ISNULL(SUM(CantidadBins), 0) FROM Lotes WHERE (@finDeMes IS NULL OR FechaIngreso <= @finDeMes)",
            ("@finDeMes", (object?)finDeMes ?? DBNull.Value));
    }

    public async Task<int> GetPalletsWithoutClientAsync(DateTime? referenceDate = null)
    {
        // Métrica de estado operativo actual: no se filtra por período, ya que
        // "sin cliente asignado" es una condición del momento, no histórica.
        return await ExecuteScalarIntAsync(
            "SELECT COUNT(*) FROM Pallets WHERE Cliente IS NULL OR Cliente = ''");
    }

    public async Task<decimal> GetProjectedGrowthAsync(DateTime? referenceDate = null)
    {
        // Compara el mes seleccionado contra el anterior, usando los mismos datos
        // que alimentan el gráfico de volcados mensuales — nada hardcodeado.
        var monthly = await GetMonthlyVolcadosAsync(referenceDate);
        if (monthly.Count < 2) return 0m;

        var ultimo = monthly[^1].Kgs;
        var anterior = monthly[^2].Kgs;
        if (anterior == 0) return 0m;

        return Math.Round((decimal)(ultimo - anterior) / anterior * 100m, 1);
    }

    /// <summary>
    /// Detalle de volcados por productor/variedad DEL MES seleccionado (no acumulado).
    /// Si no se pasa fecha, muestra el mes actual.
    /// </summary>
    public async Task<List<VolcadoDetalle>> GetVolcadosDetailAsync(DateTime? referenceDate = null, int take = 20)
    {
        var fin = FinDeMes(referenceDate) ?? FinDeMes(DateTime.Now)!.Value;
        var inicio = new DateTime(fin.Year, fin.Month, 1);

        const string sql = @"
            SELECT TOP (@take)
                p.Nombre AS Productor,
                v.Nombre AS Variedad,
                SUM(vo.PesoKilos) AS KgsVolcados,
                SUM(vo.CantidadBins) AS Bins,
                l.Zafra AS Zafra
            FROM Volcados vo
            INNER JOIN Lotes l ON l.Id = vo.LoteId
            INNER JOIN Productores p ON p.Id = l.IdProductor
            INNER JOIN Variedades v ON v.IdVariedad = l.IdVariedad
            WHERE vo.FechaVolcado >= @inicio AND vo.FechaVolcado <= @fin
            GROUP BY p.Nombre, v.Nombre, l.Zafra
            ORDER BY KgsVolcados DESC";

        var result = new List<VolcadoDetalle>();

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParam(command, "@take", take);
        AddParam(command, "@inicio", inicio);
        AddParam(command, "@fin", fin);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new VolcadoDetalle
            {
                Productor = reader.IsDBNull(0) ? "Sin productor" : reader.GetString(0),
                Variedad = reader.IsDBNull(1) ? "Sin variedad" : reader.GetString(1),
                KgsVolcados = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2)),
                Bins = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                Zafra = reader.IsDBNull(4) ? "Sin zafra" : reader.GetString(4)
            });
        }

        return result;
    }

    /// <summary>Último instante del mes de la fecha dada (23:59:59.997), o null si la fecha es null.</summary>
    private static DateTime? FinDeMes(DateTime? date)
    {
        if (date is null) return null;
        var d = date.Value;
        var primerDiaSiguiente = new DateTime(d.Year, d.Month, 1).AddMonths(1);
        return primerDiaSiguiente.AddMilliseconds(-3);
    }

    private static void AddParam(System.Data.Common.DbCommand command, string name, object value)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        command.Parameters.Add(param);
    }

    private async Task<int> ExecuteScalarIntAsync(string sql, params (string name, object value)[] parameters)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            AddParam(command, name, value);

        var value2 = await command.ExecuteScalarAsync();
        return value2 is null || value2 is DBNull ? 0 : Convert.ToInt32(value2);
    }
}
