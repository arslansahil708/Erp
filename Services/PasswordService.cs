using System.Security.Cryptography;

namespace ZnErp.Services;

/// <summary>PBKDF2-SHA256 password hashing. Format: v1.iterations.salt.hash (base64).</summary>
public static class PasswordService
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        try
        {
            var parts = stored.Split('.');
            if (parts.Length != 4 || parts[0] != "v1") return false;

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Password ki minimum shart. Theek ho to null, warna error message.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return "Password kam az kam 8 characters ka hona chahiye.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password mein kam az kam ek harf aur ek number hona chahiye.";
        return null;
    }
}
