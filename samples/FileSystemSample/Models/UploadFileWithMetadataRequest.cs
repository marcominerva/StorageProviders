namespace FileSystemSample.Models;

/// <summary>
/// Represents a multipart request containing a file and optional JSON-encoded metadata.
/// </summary>
public sealed record UploadFileWithMetadataRequest
{
    /// <summary>
    /// Gets the optional logical folder under which the file is stored.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    /// Gets a value indicating whether an existing file may be replaced.
    /// </summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Gets the optional JSON object containing metadata associated with the file.
    /// </summary>
    public string? JsonMetadata { get; init; }

    /// <summary>
    /// Gets the file content to upload.
    /// </summary>
    public required IFormFile File { get; init; }
}
