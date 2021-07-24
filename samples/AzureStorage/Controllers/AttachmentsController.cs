using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MimeMapping;
using StorageProvider.Abstractions;

namespace StorageSample.Controllers
{
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

        [HttpGet("list")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public IActionResult GetList(string prefix = null, [FromQuery(Name = "extension")] string[] extensions = null)
        {
            var attachments = storageProvider.EnumerateAsync(prefix, extensions);

            // If you need to get the actual list, you can use the .ToListAsync() extension method, like
            // in the following example.
            //var list = await attachments.ToListAsync();

            return Ok(attachments);
        }

        [HttpGet("exists")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesDefaultResponseType]
        public async Task<IActionResult> Exists(string fileName)
        {
            var exists = await storageProvider.ExistsAsync(fileName);
            if (exists)
            {
                return NoContent();
            }

            return NotFound();
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesDefaultResponseType]
        public async Task<IActionResult> Save([BindRequired] IFormFile attachment, string folder = null, bool overwrite = false)
        {
            using var stream = attachment.OpenReadStream();
            await storageProvider.SaveAsync(Path.Combine(folder ?? string.Empty, attachment.FileName), stream, overwrite);
            return NoContent();
        }

        [HttpGet]
        [Produces(contentType: MediaTypeNames.Application.Octet, MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesDefaultResponseType]
        public async Task<IActionResult> Get(string fileName)
        {
            var attachment = await storageProvider.ReadAsByteArrayAsync(fileName);
            if (attachment != null)
            {
                return File(attachment, MimeUtility.GetMimeMapping(fileName));
            }

            return NotFound();
        }

        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesDefaultResponseType]
        public async Task<IActionResult> Delete(string fileName)
        {
            await storageProvider.DeleteAsync(fileName);
            return NoContent();
        }
    }
}