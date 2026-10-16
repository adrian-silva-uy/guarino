using GuarinoApp.Application.Modules.Contaduria.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Contaduria.Queries.GetContextoOperativo;

/// <summary>
/// Contexto operativo de Contaduría: totales de liquidaciones (a pagar),
/// avance de cosecha, stock en playa y pallets sin cliente, calculados
/// directamente sobre las tablas fuente — con la misma lógica que sus
/// módulos, pero SIN el fallback de datos de ejemplo: si no hay datos
/// reales, los campos quedan en null para que la IA los declare faltantes.
/// </summary>
public record GetContextoOperativoQuery(int Anio) : IRequest<ContextoOperativoContaduriaDto>;
