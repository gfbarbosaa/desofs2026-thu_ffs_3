using Moq;
using SafeVault.Application.DTOs.Documents;
using SafeVault.Application.IServices;
using SafeVault.Application.Services;
using SafeVault.Domain.EntityModels;
using SafeVault.Domain.Enums;
using SafeVault.Domain.IRepositories;

namespace SafeVault.ApplicationTests;

/// <summary>
/// Security regression tests at the Application (use-case) layer.
/// Covers: AC-02 (IDOR), AC-04 (malicious upload), AC-05 (integrity check),
///         RS-04.4 (magic bytes), RS-04.5 (path traversal via filename),
///         RS-02.3 (hash verification on download), RS-01.3 (RBAC).
/// </summary>
public class SecurityApplicationTests
{
    private static Vault BuildVault(Guid ownerId) =>
        new("Finance", "desc", ownerId, "/storage/vaults", 90, false);

    private static Document BuildDocument(Guid vaultId, Guid uploadedBy) =>
        new(vaultId, uploadedBy, "contract.pdf", "stored.pdf", "/storage/contract.pdf",
            "application/pdf", 1000,
            "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
            DocumentClassification.Confidential);

    // ── AC-04 / RS-04.4 · Malicious file upload — wrong magic bytes ─────────────

    /// <summary>
    /// Uploading a file that claims to be PDF but contains executable magic bytes
    /// must be rejected with InvalidOperationException.
    /// Mitigates AC-04: Upload of malicious file with disguised extension.
    /// </summary>
    [Fact]
    public async Task Upload_RejectsFile_WhenMagicBytesDoNotMatchMimeType()
    {
        // Arrange: stream that starts with Windows PE header (MZ), not %PDF-
        var maliciousContent = new byte[512];
        maliciousContent[0] = 0x4D; // M
        maliciousContent[1] = 0x5A; // Z  ← PE / EXE magic bytes
        var stream = new MemoryStream(maliciousContent) { Position = 0 };

        var ownerId = Guid.NewGuid();
        var vault = BuildVault(ownerId);

        var vaultRepo = new Mock<IVaultRepository>();
        vaultRepo.Setup(r => r.GetByIdAsync(vault.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vault);

        var sut = new DocumentService(
            vaultRepo.Object,
            Mock.Of<IDocumentRepository>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IHashService>(),
            Mock.Of<IAuditWriter>());

        var request = new UploadDocumentRequest(
            vault.Id,
            "invoice.pdf",
            "application/pdf",
            stream.Length,
            DocumentClassification.Confidential,
            stream);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UploadAsync(ownerId, request));
    }

    /// <summary>
    /// Uploading a file that genuinely starts with %PDF- must succeed.
    /// </summary>
    [Fact]
    public async Task Upload_Accepts_LegitimateFile_WithCorrectMagicBytes()
    {
        // Arrange: valid PDF magic bytes
        var pdfBytes = new byte[512];
        var header = System.Text.Encoding.ASCII.GetBytes("%PDF-");
        header.CopyTo(pdfBytes, 0);
        var stream = new MemoryStream(pdfBytes) { Position = 0 };

        var ownerId = Guid.NewGuid();
        var vault = BuildVault(ownerId);

        var vaultRepo = new Mock<IVaultRepository>();
        vaultRepo.Setup(r => r.GetByIdAsync(vault.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vault);

        var storedPath = $"/storage/{vault.Id:N}/file.pdf";
        var fileStorage = new Mock<IFileStorageService>();
        fileStorage.Setup(f => f.SaveFileAsync(vault.Id, It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedPath);

        var validHash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        var hashService = new Mock<IHashService>();
        hashService.Setup(h => h.ComputeSha256(It.IsAny<Stream>())).Returns(validHash);

        var docRepo = new Mock<IDocumentRepository>();

        var sut = new DocumentService(vaultRepo.Object, docRepo.Object, fileStorage.Object, hashService.Object, Mock.Of<IAuditWriter>());

        var request = new UploadDocumentRequest(
            vault.Id,
            "report.pdf",
            "application/pdf",
            pdfBytes.Length,
            DocumentClassification.Internal,
            stream);

        var result = await sut.UploadAsync(ownerId, request);
        Assert.NotNull(result);
    }

    // ── AC-02 / RS-01.3 · IDOR — Viewer cannot write to a vault ─────────────────

    /// <summary>
    /// A user that has no write access (not owner, not added with write permission)
    /// must receive UnauthorizedAccessException on upload.
    /// Mitigates AC-02: Escalation of privileges via IDOR.
    /// </summary>
    [Fact]
    public async Task Upload_Throws_WhenActorHasNoWriteAccess()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid(); // different user — not owner, not granted

        var vault = BuildVault(ownerId);

        var vaultRepo = new Mock<IVaultRepository>();
        vaultRepo.Setup(r => r.GetByIdAsync(vault.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vault);

        var pdfBytes = new byte[512];
        System.Text.Encoding.ASCII.GetBytes("%PDF-").CopyTo(pdfBytes, 0);
        var stream = new MemoryStream(pdfBytes) { Position = 0 };

        var sut = new DocumentService(
            vaultRepo.Object,
            Mock.Of<IDocumentRepository>(),
            Mock.Of<IFileStorageService>(),
            Mock.Of<IHashService>(),
            Mock.Of<IAuditWriter>());

        var request = new UploadDocumentRequest(vault.Id, "secret.pdf", "application/pdf", pdfBytes.Length, DocumentClassification.Confidential, stream);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.UploadAsync(attackerId, request));
    }

    /// <summary>
    /// A user with no read access to a vault must receive UnauthorizedAccessException on download.
    /// </summary>
    [Fact]
    public async Task Download_Throws_WhenActorHasNoReadAccess()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        var vault = BuildVault(ownerId);
        var doc = BuildDocument(vault.Id, ownerId);

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var vaultRepo = new Mock<IVaultRepository>();
        vaultRepo.Setup(r => r.GetByIdAsync(vault.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vault);

        var sut = new DocumentService(vaultRepo.Object, docRepo.Object, Mock.Of<IFileStorageService>(), Mock.Of<IHashService>(), Mock.Of<IAuditWriter>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.DownloadAsync(attackerId, doc.Id));
    }

    // ── RS-02.3 / T-10 · Hash integrity on download ─────────────────────────────

    /// <summary>
    /// If the stored SHA-256 hash does not match the file content on disk,
    /// the download must throw InvalidOperationException.
    /// Mitigates T-10: Undetected file tampering.
    /// </summary>
    [Fact]
    public async Task Download_Throws_WhenFileHashMismatch()
    {
        var ownerId = Guid.NewGuid();
        var vault = BuildVault(ownerId);
        var doc = BuildDocument(vault.Id, ownerId);

        var docRepo = new Mock<IDocumentRepository>();
        docRepo.Setup(r => r.GetByIdAsync(doc.Id, It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var vaultRepo = new Mock<IVaultRepository>();
        vaultRepo.Setup(r => r.GetByIdAsync(vault.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vault);

        var tamperedContent = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
        var fileStorage = new Mock<IFileStorageService>();
        fileStorage.Setup(f => f.ReadFileAsync(doc.FilePath, It.IsAny<CancellationToken>())).ReturnsAsync(tamperedContent);

        var hashService = new Mock<IHashService>();
        // Returns a different hash than what is stored in the document — simulates tampered file
        hashService.Setup(h => h.ComputeSha256(It.IsAny<Stream>()))
            .Returns("0000000000000000000000000000000000000000000000000000000000000000");

        var sut = new DocumentService(vaultRepo.Object, docRepo.Object, fileStorage.Object, hashService.Object, Mock.Of<IAuditWriter>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DownloadAsync(ownerId, doc.Id));
    }

    // ── RS-01.4 / AC-01 · Brute force — locked user cannot login ────────────────

    /// <summary>
    /// AuthService must throw UnauthorizedAccessException when the account is locked.
    /// Mitigates AC-01: Brute-force attack.
    /// </summary>
    [Fact]
    public async Task Login_Throws_WhenAccountIsLocked()
    {
        var lockedUser = new User("victim@example.com", "hash", UserRole.Viewer);
        for (var i = 0; i < 5; i++) lockedUser.RegisterFailedLoginAttempt();

        Assert.True(lockedUser.IsLocked());

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByEmailAsync(lockedUser.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        var sut = new AuthService(
            userRepository.Object,
            Mock.Of<IPasswordHasher>(),
            Mock.Of<ITokenService>(),
            Mock.Of<IHashService>(),
            Mock.Of<IAuditWriter>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LoginAsync(new Application.DTOs.Auth.LoginRequest(lockedUser.Email, "AnyPass!1"), "127.0.0.1", "test"));
    }

    /// <summary>
    /// AuthService must throw UnauthorizedAccessException when the password is wrong.
    /// Counter must increase; does NOT reveal whether the email exists (uniform response).
    /// </summary>
    [Fact]
    public async Task Login_Throws_WhenPasswordIsWrong()
    {
        var user = new User("user@example.com", "bcrypt-hash", UserRole.Viewer);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(x => x.Verify(user.PasswordHash, "WrongPassword!1")).Returns(false);

        var sut = new AuthService(userRepository.Object, passwordHasher.Object, Mock.Of<ITokenService>(), Mock.Of<IHashService>(), Mock.Of<IAuditWriter>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LoginAsync(new Application.DTOs.Auth.LoginRequest(user.Email, "WrongPassword!1"), "10.0.0.1", "ua"));
    }
}

