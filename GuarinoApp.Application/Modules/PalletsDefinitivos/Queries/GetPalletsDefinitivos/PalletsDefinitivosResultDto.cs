using GuarinoApp.Application.Modules.PalletsDefinitivos.Dtos;

namespace GuarinoApp.Application.Modules.PalletsDefinitivos.Queries.GetPalletsDefinitivos;

/// <summary>
/// Envuelve el resultado de GetPalletsDefinitivosQuery junto con el flag EsDummy del
/// DataSourceFooter. Los fallbacks de ejemplo se eliminaron — cero datos
/// inventados: hoy EsDummy es siempre false. El flag se conserva por contrato con
/// PalletsDefinitivos.razor.
/// </summary>
public record PalletsDefinitivosResultDto(List<PalletDefinitivoDto> Pallets, bool EsDummy);
