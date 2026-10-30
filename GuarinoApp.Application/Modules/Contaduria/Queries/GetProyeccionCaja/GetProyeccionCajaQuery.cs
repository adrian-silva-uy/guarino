using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contaduria.Dtos;
using GuarinoApp.Application.Modules.Proyeccion.Queries.GetProyeccionZafra;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GuarinoApp.Application.Modules.Contaduria.Queries.GetProyeccionCaja;

public record GetProyeccionCajaQuery(string Escenario = "Base") : IRequest<ProyeccionCajaResultDto>;

public class GetProyeccionCajaQueryHandler : IRequestHandler<GetProyeccionCajaQuery, ProyeccionCajaResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GetProyeccionCajaQueryHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<ProyeccionCajaResultDto> Handle(GetProyeccionCajaQuery request, CancellationToken cancellationToken)
    {
        var resultado = new ProyeccionCajaResultDto { Escenario = request.Escenario };

        // 1. Datos de Contaduría (caja disponible, costo por kg, flete)
        var datosFinancieros = await _mediator.Send(
            new GuarinoApp.Application.Modules.Contaduria.Queries.GetFinancialData.GetFinancialDataQuery(),
            cancellationToken);

        // 2. Proyección de zafra (volumen proyectado)
        var zafras = await _mediator.Send(
            new GetProyeccionZafraQuery(), cancellationToken);
        var zafraProyectada = zafras?.FirstOrDefault(z => z.Estado == "Proyectado")
            ?? zafras?.FirstOrDefault();

        // 3. Liquidaciones a productores del año (salida de caja)
        var liquidacionesAnio = await _context.Lotes
            .AsNoTracking()
            .Where(l => !l.IsDeleted && l.FechaIngreso.HasValue
                && l.FechaIngreso.Value.Year == DateTime.Today.Year)
            .SumAsync(l => (decimal?)l.KilosNetos * 10, cancellationToken);

        // Factor por escenario
        var factorEscenario = request.Escenario switch
        {
            "Pesimista" => 0.8m,
            "Optimista" => 1.2m,
            _ => 1m // Base
        };

        // Saldo inicial = flujo de caja de Contaduría
        var saldoInicial = datosFinancieros?.FlujoCaja ?? 0m;
        resultado.SaldoInicialUsd = saldoInicial;

        // Si no hay datos, devolvemos vacío con aviso
        if (zafraProyectada == null || zafraProyectada.TotalKgs <= 0)
        {
            return resultado;
        }

        // 4. Generar 13 semanas
        var volumenPorSemanaBase = zafraProyectada.TotalKgs / 13m;
        var precioUsdPorKg = datosFinancieros?.CostoPorKg ?? 0m;
        var liquidacionesPorSemana = (liquidacionesAnio ?? 0m) / 13m;
        var costoFlete = datosFinancieros?.CostoFlete ?? 0m;

        var saldo = saldoInicial;
        var fechaInicio = DateTime.Today;

        for (int s = 1; s <= 13; s++)
        {
            var fechaDesde = fechaInicio.AddDays((s - 1) * 7);
            var fechaHasta = fechaDesde.AddDays(6);

            // Entradas: cobros por embarque = volumen semana × precio × factor escenario
            var entradas = volumenPorSemanaBase * precioUsdPorKg * factorEscenario;

            // Salidas distribuidas
            var salidasLiquidaciones = liquidacionesPorSemana;
            var salidasJornales = volumenPorSemanaBase * 0.05m * factorEscenario;
            var salidasFletes = (volumenPorSemanaBase / 25000m) * costoFlete * factorEscenario;
            var salidasImpuestos = entradas * 0.03m;
            var salidasOtros = volumenPorSemanaBase * 0.02m * factorEscenario;

            var totalSalidas = salidasLiquidaciones + salidasJornales + salidasFletes + salidasImpuestos + salidasOtros;
            saldo += entradas - totalSalidas;

            resultado.Datos.Add(new FlujoCajaSemanaDto
            {
                Semana = s,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Escenario = request.Escenario,
                EntradasUsd = Math.Round(entradas, 2),
                SalidasLiquidacionesQuintaUsd = Math.Round(salidasLiquidaciones, 2),
                SalidasJornalesUsd = Math.Round(salidasJornales, 2),
                SalidasFletesUsd = Math.Round(salidasFletes, 2),
                SalidasImpuestosUsd = Math.Round(salidasImpuestos, 2),
                SalidasOtrosUsd = Math.Round(salidasOtros, 2),
                SaldoAcumuladoUsd = Math.Round(saldo, 2),
                EsReal = false
            });
        }

        return resultado;
    }
}
