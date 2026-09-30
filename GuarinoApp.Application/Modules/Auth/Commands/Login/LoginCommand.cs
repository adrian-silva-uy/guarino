using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Auth.Commands.Login;

/// <summary>Inicio de sesión con usuario y contraseña.</summary>
public record LoginCommand(string UserName, string Password) : IRequest<AuthSessionDto>;
