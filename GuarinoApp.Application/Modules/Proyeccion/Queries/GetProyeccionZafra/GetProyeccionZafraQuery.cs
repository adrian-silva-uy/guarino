using GuarinoApp.Application.Modules.Proyeccion.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;

/// <summary>
/// PorcentajeCrecimientoManual: si el Contador lo completa, se usa tal cual; si no, se calcula
/// del promedio de variación interanual real.
///
/// Los 4 parámetros de abajo controlan el "Impacto Operativo" (bins/camiones/horas de empaque)
/// que antes estaba hardcodeado en el .razor (25kg/bin, 25tn/camión, 10tn/hora, margen de riesgo
/// 10%). Todos son opcionales: si no se completan, el handler usa el default más razonable
/// disponible -- KgPromedioPorBin se CALCULA de los Lotes reales (no es un supuesto); los otros
/// tres no tienen fuente de datos real en el sistema todavía (no hay tabla de flota de camiones
/// ni de rendimiento de línea de empaque), así que quedan como parámetro ajustable con un default
/// documentado, no como una cifra inventada silenciosa.
/// </summary>
public record GetProyeccionZafraQuery(
    decimal? PorcentajeCrecimientoManual = null,
    decimal? KgPromedioPorBin = null,
    decimal? ToneladasPorCamion = null,
    decimal? ToneladasPorHoraEmpaque = null,
    decimal? MargenRiesgoCapacidadPorcentual = null
) : IRequest<List<ZafraDto>>;
