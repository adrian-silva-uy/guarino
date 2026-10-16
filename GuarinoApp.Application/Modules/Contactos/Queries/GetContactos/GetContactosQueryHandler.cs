using GuarinoApp.Application.Common.Interfaces;
using GuarinoApp.Application.Modules.Contactos.Dtos;
using GuarinoApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuarinoApp.Application.Modules.Contactos.Queries.GetContactos;

public class GetContactosQueryHandler : IRequestHandler<GetContactosQuery, List<ContactoDto>>
    {
    private readonly IApplicationDbContext _context;
    private readonly ILogger<GetContactosQueryHandler> _logger;

    public GetContactosQueryHandler(IApplicationDbContext context, ILogger<GetContactosQueryHandler> logger)
        {
        _context = context;
        _logger = logger;
        }

    public async Task<List<ContactoDto>> Handle(GetContactosQuery request, CancellationToken cancellationToken)
        {
        try
            {
            var query = _context.Contactos.AsNoTracking().Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(request.Estado))
                query = query.Where(c => c.Estado == request.Estado);

            if (!string.IsNullOrWhiteSpace(request.Busqueda))
                {
                // Tope de longitud para el texto libre de búsqueda (defensa en
                // profundidad junto al InputSanitizationBehavior central).
                var texto = request.Busqueda.Trim();
                if (texto.Length > 100)
                    texto = texto[..100];
                // EF Core LINQ → SQL parametrizado (LIKE con parámetro), nunca SQL
                // concatenado: un texto con comillas, -- o ; no puede alterar la consulta.
                query = query.Where(c =>
                    c.Nombre.Contains(texto) ||
                    c.Empresa.Contains(texto) ||
                    (c.Pais != null && c.Pais.Contains(texto)));
                }

            if (!string.IsNullOrWhiteSpace(request.Estado))
                {
                // Allowlist: solo los estados conocidos; cualquier otro valor
                // (texto libre inyectado en el select) se ignora.
                var estado = request.Estado.Trim();
                if (Array.IndexOf(ContactoEstados.Todos, estado) >= 0)
                    query = query.Where(c => c.Estado == estado);
                }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new ContactoDto
                    {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Empresa = c.Empresa,
                    Pais = c.Pais,
                    Email = c.Email,
                    Telefono = c.Telefono,
                    Interes = c.Interes,
                    Notas = c.Notas,
                    Estado = c.Estado,
                    FechaAlta = c.CreatedAt,
                    CreadoPor = c.CreatedBy,
                    FechaModificacion = c.UpdatedAt,
                    Origen = c.Origen
                    })
                .ToListAsync(cancellationToken);
            }
        catch (Exception ex)
            {
            // Misma tolerancia que Transportistas/Lugares: la pantalla no se
            // rompe si la consulta falla; se registra y se lista vacío.
            _logger.LogError(ex, "No se pudieron leer los contactos.");
            return new List<ContactoDto>();
            }
        }
    }
