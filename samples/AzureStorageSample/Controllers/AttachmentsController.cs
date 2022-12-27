using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MimeMapping;
using StorageProviders;

namespace StorageSample.Controllers;

[Route("api/attachments")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
public class AttachmentsController : ControllerBase
{
    private readonly IStorageProvider storageProvider;

    public AttachmentsController(IStorageProvider storageProvider)
    {
        this.storageProvider = storageProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public IActionResult GetList(string? prefix = null, [FromQuery(Name = "extension")] string[] extensions = null!)
    {
        var attachments = storageProvider.EnumerateAsync(prefix, extensions);

        // If you need to get the actual list, you can use the .ToListAsync() extension method:
        //var list = await attachments.ToListAsync();

        return Ok(attachments);
    }

    [HttpGet("exists")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> Exists([BindRequired] string fileName)
    {
        var exists = await storageProvider.ExistsAsync(fileName);
        if (exists)
        {
            return NoContent();
        }

        return NotFound();
    }

    [HttpGet("fullpath")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> GetFullPath([BindRequired] string fileName)
    {
        var fullPath = await storageProvider.GetFullPathAsync(fileName);
        return Ok(fullPath);
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(StorageFileInfo), StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> GetProperties([BindRequired] string fileName)
    {
        var properties = await storageProvider.GetPropertiesAsync(fileName);
        return Ok(properties);
    }

    [HttpGet("readuri")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> GetReadAccessUri([BindRequired] string fileName, [BindRequired] DateTime expirationDate)
    {
        var readUri = await storageProvider.GetReadAccessUriAsync(fileName, expirationDate);
        return Ok(readUri);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> Save([BindRequired] IFormFile attachment, string? folder = null, bool overwrite = false)
    {
        using var stream = attachment.OpenReadStream();
        await storageProvider.SaveAsync(Path.Combine(folder ?? string.Empty, attachment.FileName), stream, overwrite);
        return NoContent();
    }

    [HttpGet("content")]
    [Produces(contentType: MediaTypeNames.Application.Octet, MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> Get([BindRequired] string fileName)
    {
        var attachment = await storageProvider.ReadAsByteArrayAsync(fileName);
        if (attachment is not null)
        {
            return File(attachment, MimeUtility.GetMimeMapping(fileName));
        }

        return NotFound();
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesDefaultResponseType]
    public async Task<IActionResult> Delete([BindRequired] string fileName)
    {
        await storageProvider.DeleteAsync(fileName);
        return NoContent();
    }
}
