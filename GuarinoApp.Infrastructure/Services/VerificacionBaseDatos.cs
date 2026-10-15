namespace GuarinoApp.Infrastructure.Services;

/// <summary>Resultado de una verificación de conexión a la base de datos
/// (análogo a ResultadoVerificacion de la sanación de Ollama).</summary>
/// <param name="Diagnostico">Ver DiagnosticoBaseDatos.</param>
/// <param name="Servidor">Servidor y base de la cadena de conexión (sin
/// credenciales: solo DataSource/InitialCatalog) para logs y correos.</param>
/// <param name="Detalle">Explicación legible del resultado (último error SQL,
/// causa de no-gestión, etc.).</param>
public record VerificacionBaseDatos(DiagnosticoBaseDatos Diagnostico, string Servidor, string Detalle);
