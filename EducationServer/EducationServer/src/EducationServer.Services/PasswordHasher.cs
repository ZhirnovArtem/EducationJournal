using System.Security.Cryptography;
using System.Text;
using EducationServer.Services.Abstractions;
using Konscious.Security.Cryptography;

namespace EducationServer.Services;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemorySize = 65536;   
    private const int Parallelism = 4;

    public byte[] Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Argon2(password, salt);

        var combined = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, combined, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, combined, SaltSize, HashSize);
        return combined;
    }

    public bool Verify(string password, byte[] stored)
    {
        if (stored.Length != SaltSize + HashSize) return false;

        var salt = stored.AsSpan(0, SaltSize).ToArray();
        var expected = stored.AsSpan(SaltSize, HashSize).ToArray();
        var actual = Argon2(password, salt);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] Argon2(string password, byte[] salt)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };
        return argon.GetBytes(HashSize);
    }
}