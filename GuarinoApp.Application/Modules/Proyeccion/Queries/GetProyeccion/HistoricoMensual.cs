namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccion;

/// <summary>Volumen mensual histórico del que se alimenta la proyección.</summary>
internal sealed record HistoricoMensual(int Año, int Mes, decimal KgsVolcados);
