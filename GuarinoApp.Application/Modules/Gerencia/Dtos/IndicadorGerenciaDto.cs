namespace GuarinoApp.Application.Modules.Gerencia.Dtos;
public class IndicadorGerenciaDto
    {
    public string? Indicador { get; set; }
    public decimal Valor { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Categoria { get; set; }
    // Unidad de medida del valor (kg, pallets, kg/h...): la muestra la tarjeta y
    // viaja en el contexto de IA para que el análisis nunca confunda escalas
    // (p. ej. presentar bins como kg).
    public string? Unidad { get; set; }
    }
