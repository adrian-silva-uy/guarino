using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Auth.Commands.UpdateUser;

/// <summary>
/// Actualiza el correo de un usuario (solo Admin). El correo define a dónde
/// se le envían los reportes que pide desde la interfaz; vacío = los reportes
/// van al administrador.
/// </summary>
public record UpdateUserEmailCommand(int UserId, string? Email) : IRequest<AuthResultDto>;
