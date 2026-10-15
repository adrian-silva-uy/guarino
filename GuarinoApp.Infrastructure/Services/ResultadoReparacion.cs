namespace GuarinoApp.Infrastructure.Services;

/// <summary>Resultado de un intento de reparación del servicio de IA local.</summary>
internal sealed record ResultadoReparacion(bool Exitosa, string Detalle);
