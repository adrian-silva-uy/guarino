using GuarinoApp.Application.Modules.Auth.Dtos;
using MediatR;

namespace GuarinoApp.Application.Modules.Auth.Commands.CreateUser;

/// <summary>
/// Da de alta un usuario desde el panel de Usuarios (solo Admin).
/// La contraseña nunca se guarda ni viaja en claro: se hashea con PBKDF2
/// en el handler. El correo es opcional y define a dónde van sus reportes.
/// </summary>
public record CreateUserCommand(string UserName, string FullName, string Password, string Role, string? Email = null)
    : IRequest<AuthResultDto>;
