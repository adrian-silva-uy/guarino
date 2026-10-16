using MediatR;

namespace GuarinoApp.Application.Common.Behaviors;

/// <summary>
/// Exención de la puerta de serialización para requests cuyo handler NO deja
/// operaciones abiertas sobre el DbContext del circuito y puede demorar
/// (llamadas HTTP con reintentos): retener la puerta frenaría todas las cargas
/// de la página durante el timeout. Sus envíos internos MediatR sí se encolan
/// normalmente (la marca solo existe dentro de esta cadena de ejecución).
/// </summary>
public interface IRequestExentoPuertaDb
{
}

/// <summary>
/// Cola por circuito para los requests MediatR: garantiza que dos envíos
/// simultáneos (carga inicial + clic del usuario, o una página anterior todavía
/// en vuelo tras navegar) nunca usen el mismo DbContext a la vez. Sin esto,
/// EF lanzaba "A second operation was started on this context instance" y la
/// página quedaba en silencio con "0 registros" o datos a medias.
/// </summary>
public sealed class SerializacionDbContextBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly PuertaSerializacionDbContext _puerta;

    public SerializacionDbContextBehavior(PuertaSerializacionDbContext puerta) => _puerta = puerta;

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IRequestExentoPuertaDb)
            return next();

        return _puerta.EjecutarAsync(() => next());
    }
}
