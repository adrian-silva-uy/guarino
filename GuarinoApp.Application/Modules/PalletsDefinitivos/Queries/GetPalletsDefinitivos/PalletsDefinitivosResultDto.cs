using GuarinoApp.Application.Modules.PalletsDefinitivos.Dtos;

namespace GuarinoApp.Application.Modules.PalletsDefinitivos.Queries.GetPalletsDefinitivos;

/// <summary>
/// Envuelve el resultado de GetPalletsDefinitivosQuery junto con si esos datos son reales
/// (vinieron de la base) o son el fallback de ejemplo (GetDummyData). Sigue el mismo patrón
/// que PalletsResultDto (módulo Stock) para que la página avise al usuario cuando está viendo
/// datos de prueba en lugar de filas reales del rango de fechas consultado.
/// </summary>
public record PalletsDefinitivosResultDto(List<PalletDefinitivoDto> Pallets, bool EsDummy);
