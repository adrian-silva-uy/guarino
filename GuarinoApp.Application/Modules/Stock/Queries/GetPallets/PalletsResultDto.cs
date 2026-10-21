namespace GuarinoApp.Application.Modules.Stock.Queries.GetPallets;

/// <summary>
/// Envuelve el resultado de GetPalletsQuery junto con el flag EsDummy del DataSourceFooter.
/// Los fallbacks de ejemplo se eliminaron (cero datos inventados): hoy EsDummy es siempre
/// false y el footer informa "Datos sincronizados". El flag se conserva por contrato con
/// stockgeneral.razor.
/// </summary>
public record PalletsResultDto(List<PalletDto> Pallets, bool EsDummy);
