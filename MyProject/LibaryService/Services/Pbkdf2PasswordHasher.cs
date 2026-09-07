using System.Security.Cryptography;
using LibaryCore.Services;

namespace LibaryService.Services;

/// <summary>
/// PBKDF2 (SHA-256) password hasher built on the BCL only - no external dependency. The stored
/// string is <c>{iterations}.{salt}.{subkey}</c>, both parts Base64. Verification reads the
/// parameters back out of the stored value, so the work factor can be raised later without
/// invalidating existing hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;      // 128-bit salt
    private const int KeySize = 32;       // 256-bit derived key
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations < 1)
            return false;

        byte[] salt, expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedSubkey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] actualSubkey =
            Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
