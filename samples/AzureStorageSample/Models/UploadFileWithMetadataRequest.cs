namespace AzureStorageSample.Models;

public class UploadFileWithMetadataRequest
{
    public string? Folder { get; set; }
    public bool Overwrite { get; set; } = false;
    public string JsonMetadata { get; set; } = null!;
    public IFormFile File { get; set; } = null!;
}
