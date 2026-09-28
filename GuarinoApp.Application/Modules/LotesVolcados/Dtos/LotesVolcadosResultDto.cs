namespace GuarinoApp.Application.Modules.LotesVolcados.Dtos;

/// <summary>
/// Envuelve el resultado de GetLotesVolcadosQuery junto con si los datos son reales (de
/// LotesEmboquille) o el fallback de ejemplo
/// </summary>
public record LotesVolcadosResultDto(List<LoteVolcadoDto> Lotes, bool EsDummy);
