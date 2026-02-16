namespace AzureStorageSample.Models;

/// <summary>
/// Represents a request to upload a single file together with JSON-encoded metadata.
/// </summary>
/// <remarks>
/// This model is typically used as the body of an HTTP endpoint that accepts
/// <c>multipart/form-data</c> for the file content and an additional field containing
/// JSON metadata. The server implementation can deserialize <see cref="JsonMetadata" />
/// into a strongly typed object before persisting the file to Azure Storage.
/// </remarks>
public class UploadFileWithMetadataRequest
{
    /// <summary>
    /// Gets or sets the optional folder or virtual path under which the file should be stored.
    /// </summary>
    /// <remarks>
    /// When <see langword="null" /> or empty, the file is stored at the root of the target
    /// container or share. Implementations may normalize this value to ensure consistent
    /// path separators.
    /// </remarks>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an existing file with the same name may be overwritten.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false" />, which allows the server to protect
    /// existing data by rejecting uploads that would collide with an existing blob or file.
    /// When set to <see langword="true" />, the server is expected to replace any existing
    /// content at the target location.
    /// </remarks>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// Gets or sets the JSON payload that contains arbitrary metadata describing the uploaded file.
    /// </summary>
    /// <remarks>
    /// The metadata is provided as a raw JSON string so that callers can send flexible
    /// key/value data without the API having to define a rigid schema. Server-side code
    /// can parse this JSON into a domain-specific type as needed.
    /// </remarks>
    public string JsonMetadata { get; set; } = null!;

    /// <summary>
    /// Gets or sets the file content to upload.
    /// </summary>
    /// <remarks>
    /// This is typically bound from an HTTP <c>multipart/form-data</c> request using MVC model
    /// binding. The value is required and must not be <see langword="null" /> when processing
    /// an upload request.
    /// </remarks>
    public IFormFile File { get; set; } = null!;
}
