namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Valida que una consulta del Asistente pertenezca al dominio de negocio y
/// mantiene los accesos rápidos personalizados del usuario.
/// </summary>
public interface IBusinessQueryService
{
    Task<BusinessQueryValidationResult> ValidateAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task RegisterAcceptedAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FrequentBusinessQuery>> GetFrequentAsync(
        int limit = 8,
        CancellationToken cancellationToken = default);
}

public sealed record BusinessQueryValidationResult(
    bool IsBusinessRelated,
    string? RejectionMessage = null);

public sealed record FrequentBusinessQuery(
    string Text,
    int UsageCount);
