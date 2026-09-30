using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Auth.Queries.GetUsers;

/// <summary>Lista los usuarios de la aplicación (pantalla de administración).</summary>
public record GetUsersQuery : IRequest<List<AppUserDto>>;
