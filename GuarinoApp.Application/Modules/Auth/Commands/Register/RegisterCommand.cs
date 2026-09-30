using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Auth.Commands.Register;

/// <summary>
/// Registro de un usuario nuevo. El primer usuario de la base siempre recibe
/// el rol Gerencia; los siguientes, el rol por defecto de la configuración.
/// </summary>
public record RegisterCommand(string UserName, string FullName, string Password) : IRequest<AuthSessionDto>;
