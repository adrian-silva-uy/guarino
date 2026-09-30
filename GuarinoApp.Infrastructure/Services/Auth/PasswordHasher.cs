using GuarinoApp.Application.Common.Interfaces;
using System.Security.Cryptography;

namespace GuarinoApp.Infrastructure.Services.Auth;

/// <summary>
/// Hash de contraseñas con PBKDF2-SHA256 (100.000 iteraciones).
/// El hash se almacena como "v1$iteraciones$salt$clave" y se verifica en
/// tiempo constante para evitar ataques por timing.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const string Prefix = "v1";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
