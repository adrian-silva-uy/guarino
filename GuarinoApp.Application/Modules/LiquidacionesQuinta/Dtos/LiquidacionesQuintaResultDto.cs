namespace GuarinoApp.Application.Modules.LiquidacionesQuinta.Dtos;

public record LiquidacionesQuintaResultDto(List<LiquidacionQuintaDto> Liquidaciones, List<int> Anios, bool EsDummy);
