using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeVault.Application.DTOs.Documents;
using SafeVault.Application.IServices;
using SafeVault.Domain.Enums;

namespace SafeVault.InterfaceAdapters.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,Manager,Viewer")]
public class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpGet("vault/{vaultId:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<DocumentDto>>> GetByVault(Guid vaultId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await documentService.ListByVaultAsync(userId, vaultId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Admin,Manager")]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] UploadDocumentForm form, CancellationToken cancellationToken)
    {
        await using var stream = form.File.OpenReadStream();
        var request = new UploadDocumentRequest(form.VaultId, form.File.FileName, form.File.ContentType, form.File.Length, form.Classification, stream);
        var userId = User.GetRequiredUserId();

        var result = await documentService.UploadAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{documentId:guid}/versions")]
    [Authorize(Roles = "Admin,Manager")]
    [RequestSizeLimit(104_857_600)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentDto>> UploadNewVersion(Guid documentId, [FromForm] UploadDocumentVersionForm form, CancellationToken cancellationToken)
    {
        await using var stream = form.File.OpenReadStream();
        var request = new UploadDocumentRequest(form.VaultId, form.File.FileName, form.File.ContentType, form.File.Length, form.Classification, stream);
        var userId = User.GetRequiredUserId();

        var result = await documentService.UploadNewVersionAsync(userId, documentId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{documentId:guid}/download")]
    public async Task<IActionResult> Download(Guid documentId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await documentService.DownloadAsync(userId, documentId, cancellationToken);
        return File(result.Content, result.MimeType, result.FileName);
    }

    [HttpDelete("{documentId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await documentService.SoftDeleteAsync(userId, documentId, cancellationToken);
        return NoContent();
    }
}
