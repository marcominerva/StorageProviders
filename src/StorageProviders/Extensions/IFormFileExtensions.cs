using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AzureStorageProvider.Extensions
{
    public static class IFormFileExtensions
    {
        public static async Task<byte[]> GetContentAsByteArrayAsync(this IFormFile file)
        {
            using var readStream = file.OpenReadStream();
            using var outputStream = new MemoryStream();
            await readStream.CopyToAsync(outputStream).ConfigureAwait(false);

            var content = outputStream.ToArray();
            return content;
        }
    }
}
