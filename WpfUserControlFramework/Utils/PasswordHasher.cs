using System.Security.Cryptography;

namespace RestaurantPosWpf;

/// <summary>
/// PBKDF2-SHA256 password hashing for <c>users.password</c>. The column remains <c>text</c>
/// (client schema is unchanged) but its semantics become an encoded hash record:
/// <code>pbkdf2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 subkey&gt;</code>
/// Legacy plain-text values are recognised by <see cref="Verify"/> (string compare) and should
/// be upgraded on the next successful sign-in by writing a fresh <see cref="Hash"/>.
/// </summary>
public static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int SubkeyBytes = 32;
    private const int DefaultIterations = 100_000;
    private const string Prefix = "pbkdf2$";

    public static string Hash(string plainText)
    {
        if (plainText == null)
            throw new ArgumentNullException(nameof(plainText));

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        using var kdf = new Rfc2898DeriveBytes(plainText, salt, DefaultIterations, HashAlgorithmName.SHA256);
        var subkey = kdf.GetBytes(SubkeyBytes);
        return $"{Prefix}{DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
    }

    /// <summary>
    /// True when <paramref name="plainText"/> matches <paramref name="storedValue"/>. Accepts either a
    /// PBKDF2-encoded record or a legacy plain-text value.
    /// </summary>
    public static bool Verify(string plainText, string? storedValue)
    {
        if (plainText == null || string.IsNullOrEmpty(storedValue))
            return false;

        if (!storedValue.StartsWith(Prefix, StringComparison.Ordinal))
            return string.Equals(plainText, storedValue, StringComparison.Ordinal);

        var body = storedValue.AsSpan(Prefix.Length);
        var parts = body.ToString().Split('$');
        if (parts.Length != 3)
            return false;

        if (!int.TryParse(parts[0], out var iter) || iter <= 0)
            return false;

        byte[] salt;
        byte[] storedSub;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            storedSub = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var kdf = new Rfc2898DeriveBytes(plainText, salt, iter, HashAlgorithmName.SHA256);
        var computed = kdf.GetBytes(storedSub.Length);
        return CryptographicOperations.FixedTimeEquals(computed, storedSub);
    }

    /// <summary>True when the stored value is a PBKDF2 record (no upgrade needed).</summary>
    public static bool IsHashed(string? storedValue) =>
        !string.IsNullOrEmpty(storedValue) && storedValue!.StartsWith(Prefix, StringComparison.Ordinal);
}
