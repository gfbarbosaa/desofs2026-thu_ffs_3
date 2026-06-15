using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SafeVault.Domain.EntityModels;
using SafeVault.Domain.Enums;
using SafeVault.Infrastructure.Options;
using SafeVault.Infrastructure.Security;
using SafeVault.Infrastructure.Storage;

namespace SafeVault.InfrastructureTests;

/// <summary>
/// Security regression tests for the Infrastructure layer.
/// Covers: T-01/T-02 (JWT tampering / algo "none"), T-09 (path traversal),
///         RS-01.5 (bcrypt, not plaintext), RS-02.3 (hash not stored in plain).
/// </summary>
public class SecurityInfrastructureTests
{
    private static JwtTokenService BuildJwtService() =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "SafeVault",
            Audience = "SafeVault.Api",
            SigningKey = "TestSigningKey_MinimumLength_32Chars!",
            AccessTokenMinutes = 60
        }));

    // ── T-01 / T-02 · JWT algorithm "none" attack ────────────────────────────────

    /// <summary>
    /// A token crafted with algorithm "none" (unsigned) must be rejected by
    /// the ASP.NET JWT bearer validation pipeline.
    /// This test verifies that the JwtSecurityTokenHandler refuses to validate
    /// such a token, proving RS-01.1 mitigation at the infrastructure level.
    /// </summary>
    [Fact]
    public void JwtValidation_Rejects_AlgorithmNoneToken()
    {
        const string signingKey = "TestSigningKey_MinimumLength_32Chars!";
        const string issuer = "SafeVault";
        const string audience = "SafeVault.Api";

        // Craft a token using the "none" algorithm (no signature)
        var header = Base64UrlEncode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"sub\":\"attacker\",\"iss\":\"{issuer}\",\"aud\":\"{audience}\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}");
        var algoNoneToken = $"{header}.{payload}.";

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireSignedTokens = true       // ← key defence
        };

        var handler = new JwtSecurityTokenHandler();

        var ex = Record.Exception(() =>
            handler.ValidateToken(algoNoneToken, validationParams, out _));

        Assert.NotNull(ex); // Must throw — "none" algorithm rejected
    }

    /// <summary>
    /// A token with a tampered payload (role elevated to Admin) but valid format
    /// must be rejected because the signature is no longer valid.
    /// Mitigates T-02: Token payload tampering.
    /// </summary>
    [Fact]
    public void JwtValidation_Rejects_TamperedPayload()
    {
        var service = BuildJwtService();
        var user = new User("manager@example.com", "hash", UserRole.Manager);

        var (token, _) = service.GenerateAccessToken(user);

        // Tamper: replace payload with one claiming Admin role
        var parts = token.Split('.');
        var fakeClaims = $"{{\"sub\":\"{user.Id}\",\"role\":\"Admin\",\"exp\":{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}";
        var tamperedToken = $"{parts[0]}.{Base64UrlEncode(fakeClaims)}.{parts[2]}";

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKey_MinimumLength_32Chars!")),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        var handler = new JwtSecurityTokenHandler();
        var ex = Record.Exception(() => handler.ValidateToken(tamperedToken, validationParams, out _));

        Assert.NotNull(ex); // Signature mismatch — rejected
    }

    // ── T-09 / RS-04.5 · Path traversal in file storage ─────────────────────────

    /// <summary>
    /// Saving a file with a path traversal sequence in the filename must be blocked.
    /// FileStorageService must detect that the resolved path escapes the base directory.
    /// </summary>
    [Fact]
    public async Task FileStorage_Rejects_PathTraversalInFilename()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), $"safevault-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempBase);

        try
        {
            var options = Options.Create(new StorageOptions { BasePath = tempBase });
            var sut = new FileStorageService(options);

            // The stored filename is generated internally (UUID), but test SaveFileAsync
            // with a filename that WOULD cause traversal if it were used directly.
            // The service uses Path.GetFileName() to strip directory components.
            var traversalName = "../../../../etc/cron.d/backdoor";
            var content = new MemoryStream(new byte[] { 0x41 }); // 'A'

            // Should not throw (safe name extraction), but the stored path must be inside basePath
            var storedPath = await sut.SaveFileAsync(Guid.NewGuid(), traversalName, content);

            Assert.StartsWith(tempBase, storedPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempBase, recursive: true);
        }
    }

    /// <summary>
    /// ReadFileAsync must reject attempts to read a file outside the base storage directory.
    /// Mitigates T-09: Path traversal on download.
    /// </summary>
    [Fact]
    public async Task FileStorage_Rejects_PathTraversalOnRead()
    {
        var tempBase = Path.Combine(Path.GetTempPath(), $"safevault-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempBase);

        try
        {
            var options = Options.Create(new StorageOptions { BasePath = tempBase });
            var sut = new FileStorageService(options);

            // Build a path that is outside the base directory via ..
            var outsidePath = Path.Combine(tempBase, "..", "sensitive-file.txt");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.ReadFileAsync(outsidePath));
        }
        finally
        {
            Directory.Delete(tempBase, recursive: true);
        }
    }

    // ── RS-01.5 · bcrypt password hashing — not plaintext ────────────────────────

    /// <summary>
    /// The hashed password must NOT equal the original plaintext.
    /// Verifies that BCrypt is applied and passwords are never stored in plain.
    /// Mitigates RS-01.5 requirement: bcrypt with work factor >= 12.
    /// </summary>
    [Fact]
    public void PasswordHasher_DoesNotStore_PlaintextPassword()
    {
        var sut = new PasswordHasherService();
        const string password = "Str0ngP@ssword!";

        var hash = sut.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.StartsWith("$2", hash); // BCrypt always starts with $2a$ or $2b$
    }

    /// <summary>
    /// The hash contains the work-factor indicator.
    /// A work factor of 12 means the hash starts with "$2a$12$" or "$2b$12$".
    /// Mitigates RS-01.5: cost factor >= 12 required.
    /// </summary>
    [Fact]
    public void PasswordHasher_UsesWorkFactor12()
    {
        var sut = new PasswordHasherService();
        var hash = sut.Hash("TestP@ssw0rd!");

        // BCrypt format: $2b$<cost>$<22-char salt><31-char hash>
        Assert.Contains("$12$", hash);
    }

    /// <summary>
    /// Two calls with the same password produce different hashes (salt uniqueness).
    /// Prevents rainbow table attacks.
    /// </summary>
    [Fact]
    public void PasswordHasher_ProducesDifferentHashesForSameInput()
    {
        var sut = new PasswordHasherService();
        const string password = "Str0ngP@ssword!";

        var hash1 = sut.Hash(password);
        var hash2 = sut.Hash(password);

        Assert.NotEqual(hash1, hash2);
    }

    /// <summary>
    /// Verify returns true for the correct password.
    /// </summary>
    [Fact]
    public void PasswordHasher_Verify_ReturnsTrue_ForCorrectPassword()
    {
        var sut = new PasswordHasherService();
        const string password = "CorrectP@ss1";
        var hash = sut.Hash(password);

        Assert.True(sut.Verify(hash, password));
    }

    /// <summary>
    /// Verify returns false for a wrong password.
    /// Prevents constant-time confusion between hash comparison methods.
    /// </summary>
    [Fact]
    public void PasswordHasher_Verify_ReturnsFalse_ForWrongPassword()
    {
        var sut = new PasswordHasherService();
        var hash = sut.Hash("CorrectP@ss1");

        Assert.False(sut.Verify(hash, "WrongP@ss1!"));
    }

    // ── Helper ──────────────────────────────────────────────────────────────────

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
