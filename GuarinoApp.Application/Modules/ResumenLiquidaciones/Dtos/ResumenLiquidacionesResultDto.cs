namespace GuarinoApp.Application.Modules.ResumenLiquidaciones.Dtos;

public record ResumenLiquidacionesResultDto(List<ResumenLiquidacionDto> Liquidaciones, List<int> Anios, bool EsDummy);
