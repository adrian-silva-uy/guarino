namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;

/// <summary>Volumen mensual agregado del histórico de la zafra.</summary>
internal sealed record MesVolumen(int Year, int Month, decimal TotalKilos);
