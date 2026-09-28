namespace GuarinoApp.Application.Modules.Stock.Queries.GetPallets;

/// <summary>
/// Envuelve el resultado de GetPalletsQuery junto con si esos datos son reales (vinieron de la
/// base) o son el fallback de ejemplo (GetDummyData). Existe para que stockgeneral.razor pueda
/// mostrar EsDummy de verdad en el DataSourceFooter, en vez de un "true" fijo que no reflejaba
/// el origen real de los datos.
/// </summary>
public record PalletsResultDto(List<PalletDto> Pallets, bool EsDummy);
