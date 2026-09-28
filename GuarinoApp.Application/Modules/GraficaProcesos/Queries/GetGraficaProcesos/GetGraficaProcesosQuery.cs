using GuarinoApp.Application.Modules.GraficaProcesos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.GraficaProcesos.Queries.GetGraficaProcesos
    {
    public class GetGraficaProcesosQuery : IRequest<List<GraficaProcesosDto>>
        {
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        }
    }
