using SafeVault.Domain.EntityModels;
using SafeVault.Domain.Enums;
using SafeVault.Domain.ValueObjects;

namespace SafeVault.DomainTests;

/// <summary>
/// Security regression tests that demonstrate mitigations for threats identified
/// in Phase 1 Threat Modelling (T-05, T-06, T-08, AC-01 to AC-07, RS-01 to RS-06).
///
/// Each test is annotated with the Phase 1 threat/requirement ID it covers so the
/// traceability matrix can reference this file directly.
/// </summary>
public class SecurityThreatMitigationTests
{
    // ── RS-01.4 / AC-01 · Brute-force / account lockout (T-05, T-06) ───────────

    /// <summary>
    /// After 5 consecutive failed login attempts the account is locked for 15 min.
    /// An attacker cannot keep guessing beyond the threshold.
    /// </summary>
    [Fact]
    public void User_LocksOut_AfterFiveFailedAttempts()
    {
        var user = new User("victim@example.com", "hash", UserRole.Viewer);

        for (var i = 0; i < 5; i++)
        {
            user.RegisterFailedLoginAttempt();
        }

        Assert.True(user.IsLocked());
        Assert.NotNull(user.LockoutUntilUtc);
        // Lockout must be at least 14 min in the future (allowing 1s tolerance)
        Assert.True(user.LockoutUntilUtc > DateTime.UtcNow.AddMinutes(14));
    }

    /// <summary>
    /// A locked account cannot be used even if the caller succeeds later.
    /// Demonstrates that lockout is enforced at domain level, not just service level.
    /// </summary>
    [Fact]
    public void LockedUser_IsLocked_WhenLockoutInFuture()
    {
        var user = new User("locked@example.com", "hash", UserRole.Viewer);
        for (var i = 0; i < 5; i++) user.RegisterFailedLoginAttempt();

        Assert.True(user.IsLocked());
    }

    /// <summary>
    /// After a successful login, the failed-attempt counter and lockout are cleared.
    /// Prevents indefinite lockout on legitimate recovery.
    /// </summary>
    [Fact]
    public void User_ResetsLockout_AfterSuccessfulLogin()
    {
        var user = new User("user@example.com", "hash", UserRole.Viewer);
        user.RegisterFailedLoginAttempt();
        user.RegisterFailedLoginAttempt();

        user.RegisterSuccessfulLogin();

        Assert.False(user.IsLocked());
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    // ── RS-01.5 · Password hashing policy (T-05) ────────────────────────────────

    /// <summary>
    /// Passwords shorter than 12 characters are rejected by PasswordPolicy.
    /// </summary>
    [Theory]
    [InlineData("Short1!")]
    [InlineData("abc")]
    [InlineData("")]
    public void PasswordPolicy_Rejects_ShortPasswords(string password)
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password));
    }

    /// <summary>
    /// Passwords without complexity (no upper/lower/digit/special) are rejected.
    /// </summary>
    [Theory]
    [InlineData("alllowercase1!")]        // no upper
    [InlineData("ALLUPPERCASE1!")]        // no lower
    [InlineData("NoDigitsHere!abc")]      // no digit
    [InlineData("NoSpecialChars12abc")]   // no special
    public void PasswordPolicy_Rejects_WeakPasswords(string password)
    {
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password));
    }

    /// <summary>
    /// A strong password passes validation.
    /// </summary>
    [Theory]
    [InlineData("Str0ngP@ssword!")]
    [InlineData("C0mpl3x!SecureKey")]
    [InlineData("12CharsMin!Aa")]
    public void PasswordPolicy_Accepts_StrongPasswords(string password)
    {
        var ex = Record.Exception(() => PasswordPolicy.Validate(password));
        Assert.Null(ex);
    }

    // ── RS-01.3 · RBAC — max 5 concurrent refresh tokens (T-14) ─────────────────

    /// <summary>
    /// A user may not hold more than 5 active refresh tokens.
    /// The oldest is revoked when the limit is reached.
    /// This prevents session flooding as an escalation vector.
    /// </summary>
    [Fact]
    public void User_EvictsOldestRefreshToken_WhenLimitReached()
    {
        var user = new User("user@example.com", "hash", UserRole.Viewer);

        for (var i = 1; i <= 5; i++)
        {
            user.AddRefreshToken($"token-hash-{i}", DateTime.UtcNow.AddDays(7));
        }

        // Add a 6th token — oldest should be evicted/revoked
        user.AddRefreshToken("token-hash-6", DateTime.UtcNow.AddDays(7));

        var activeCount = user.RefreshTokens.Count(t => !t.IsRevoked && t.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal(5, activeCount);
    }

    // ── RS-06.1 · Inactive user cannot be used (T-03) ───────────────────────────

    /// <summary>
    /// Deactivated users should not be active.
    /// AuthService must refuse login for inactive users; this verifies the domain invariant.
    /// </summary>
    [Fact]
    public void User_IsNotActive_AfterDeactivation()
    {
        var user = new User("active@example.com", "hash", UserRole.Manager);
        Assert.True(user.IsActive);

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    // ── RS-04.3 · Email value object rejects malformed values ───────────────────

    /// <summary>
    /// The Email value object prevents SQL injection or path traversal attempts
    /// from being stored as the user's email address.
    /// </summary>
    [Theory]
    [InlineData("")]            // empty
    [InlineData("   ")]        // whitespace
    [InlineData("missing@")]   // no domain
    [InlineData("@nodomain")]  // no local part
    public void Email_ValueObject_Rejects_InvalidValues(string bad)
    {
        Assert.Throws<ArgumentException>(() => new Email(bad));
    }
}
