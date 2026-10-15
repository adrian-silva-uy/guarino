using GuarinoApp.Application.Modules.Contactos.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Contactos.Queries.GetContactos;

/// <summary>Lista de contactos con filtros opcionales (búsqueda y estado).</summary>
public record GetContactosQuery(string? Busqueda = null, string? Estado = null) : IRequest<List<ContactoDto>>;
