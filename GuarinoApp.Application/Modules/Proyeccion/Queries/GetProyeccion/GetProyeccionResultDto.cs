using GuarinoApp.Application.Modules.Proyeccion.Dtos;

namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccion;

/// <summary>
/// Envuelve el resultado de GetProyeccionQuery: los datos en sí, más si son
/// reales (basados en LotesEmboquille) o de ejemplo (GenerateDummyProjection,
/// usado cuando no hay histórico todavía o falló la consulta). Antes la UI
/// (Proyeccion.razor) mostraba "EsDummy=true" hardcodeado siempre, sin
/// importar si los datos eran reales — con esto la UI puede reflejar la
/// verdad.
/// </summary>
public class GetProyeccionResultDto
    {
    public List<ProyeccionDto> Datos { get; set; } = new();
    public bool EsDummy { get; set; }
    }