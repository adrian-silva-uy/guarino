using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Auth.Commands.UpdateUser;

/// <summary>Cambia el rol y/o el estado de un usuario (solo Gerencia).</summary>
public record UpdateUserCommand(int UserId, string Role, bool IsActive) : IRequest<AuthResultDto>;
