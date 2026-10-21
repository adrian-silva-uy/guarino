namespace GuarinoApp.Application.Common;

/// <summary>
/// Fuente única de verdad de los nombres de rol de la aplicación.
/// Ningún punto del código debe repetir los literales "Admin"/"Gerencia"/etc.:
/// los chequeos de rol (páginas, handlers server-side, skills, filtro IA,
/// servidor MCP) referencian estas constantes, y la BD debe sembrar exactamente
/// estos mismos nombres.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Gerencia = "Gerencia";
    public const string Contador = "Contador";
    public const string Operador = "Operador";

    /// <summary>Todos los roles válidos del sistema (validación de altas/ediciones).</summary>
    public static readonly IReadOnlyList<string> Todos = new[] { Admin, Gerencia, Contador, Operador };

    /// <summary>
    /// Roles con acceso a los módulos financieros (Contaduría, Facturación,
    /// Proyección de Zafra, Resumen de Liquidaciones, Resumen Mensual).
    /// </summary>
    public static readonly IReadOnlyList<string> Financieros = new[] { Admin, Gerencia, Contador };

    /// <summary>
    /// Roles con acceso a los módulos de administración (Usuarios, Auditoría,
    /// Contactos, Panel de Gerencia).
    /// </summary>
    public static readonly IReadOnlyList<string> Administracion = new[] { Admin, Gerencia };

    // Equivalentes como cadena para los atributos [Authorize(Roles = ...)],
    // que exigen una expresión constante en tiempo de compilación.
    public const string TodosAutorizacion =
        Admin + ", " + Gerencia + ", " + Contador + ", " + Operador;

    public const string FinancierosAutorizacion =
        Admin + ", " + Gerencia + ", " + Contador;

    public const string AdministracionAutorizacion =
        Admin + ", " + Gerencia;
}
