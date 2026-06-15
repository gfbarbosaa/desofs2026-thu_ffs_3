using SafeVault.Domain.Enums;

namespace SafeVault.InterfaceAdapters.Controllers;

public class UploadDocumentForm
{
    public Guid VaultId { get; set; }
    public DocumentClassification Classification { get; set; }
    public IFormFile File { get; set; } = null!;
}

public class UploadDocumentVersionForm
{
    public Guid VaultId { get; set; }
    public DocumentClassification Classification { get; set; }
    public IFormFile File { get; set; } = null!;
}
