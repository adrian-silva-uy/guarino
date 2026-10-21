namespace GuarinoApp.Application.Modules.LotesVolcados.Dtos;

/// <summary>
/// Envuelve el resultado de GetLotesVolcadosQuery. EsDummy es siempre false: los datos
/// provienen de LotesEmboquille y los fallbacks de ejemplo se eliminaron (cero datos
/// inventados); el flag se conserva por contrato con la página.
/// </summary>
public record LotesVolcadosResultDto(List<LoteVolcadoDto> Lotes, bool EsDummy);
