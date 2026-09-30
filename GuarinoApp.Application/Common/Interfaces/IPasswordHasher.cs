namespace GuarinoApp.Application.Common.Interfaces;

/// <summary>
/// Hashea y verifica contraseñas con PBKDF2. Implementado en Infrastructure
/// para que la Application no dependa de criptografía concreta.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Genera el hash de una contraseña.</summary>
    string Hash(string password);

    /// <summary>Verifica una contraseña contra un hash almacenado.</summary>
    bool Verify(string password, string hash);
}
