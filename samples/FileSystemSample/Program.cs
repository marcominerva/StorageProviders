using System.Text.Json;
using FileSystemSample.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using StorageProviders;
using StorageProviders.FileSystem;
using TinyHelpers.AspNetCore.Extensions;
using TinyHelpers.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFileSystemStorage(options =>
{
    var rootDirectory = builder.Configuration.GetValue<string>("AppSettings:RootDirectory")!;
    options.RootDirectory = Path.GetFullPath(rootDirectory, builder.Environment.ContentRootPath);
});

builder.Services.AddOpenApi(options =>
{
    options.AddDefaultProblemDetailsResponse();
});

builder.Services.AddDefaultProblemDetails();
builder.Services.AddDefaultExceptionHandler();

var app = builder.Build();
app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();

app.MapSwaggerUI(setupAction: options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", app.Environment.ApplicationName);
    options.RoutePrefix = string.Empty;
});

var attachmentsApiGroup = app.MapGroup("/api/attachments");

attachmentsApiGroup.MapGet(string.Empty, (IStorageProvider storageProvider, string? prefix = null, [FromQuery(Name = "extension")] string[] extensions = null!, CancellationToken cancellationToken = default) =>
{
    var attachments = storageProvider.EnumerateAsync(prefix, extensions, cancellationToken);
    return TypedResults.Ok(attachments);
});

attachmentsApiGroup.MapGet("exists", async Task<Results<NoContent, NotFound>> (IStorageProvider storageProvider, string fileName, CancellationToken cancellationToken) =>
{
    var exists = await storageProvider.ExistsAsync(fileName, cancellationToken);
    return exists ? TypedResults.NoContent() : TypedResults.NotFound();
});

attachmentsApiGroup.MapGet("full-path", async (IStorageProvider storageProvider, string fileName, CancellationToken cancellationToken) =>
{
    var fullPath = await storageProvider.GetFullPathAsync(fileName, cancellationToken);
    return TypedResults.Ok(fullPath);
});

attachmentsApiGroup.MapGet("info", async (IStorageProvider storageProvider, string fileName, CancellationToken cancellationToken) =>
{
    var fileInfo = await storageProvider.GetPropertiesAsync(fileName, cancellationToken);
    return TypedResults.Ok(fileInfo);
});

attachmentsApiGroup.MapPost(string.Empty, async (IFormFile file, IStorageProvider storageProvider, string? folder = null, bool overwrite = false, CancellationToken cancellationToken = default) =>
{
    await using var stream = file.OpenReadStream();
    await storageProvider.SaveAsync(Path.Combine(folder ?? string.Empty, file.FileName), stream, overwrite, cancellationToken);

    return TypedResults.NoContent();
})
.DisableAntiforgery();

attachmentsApiGroup.MapPost("upload-metadata", async (IStorageProvider storageProvider, [FromForm] UploadFileWithMetadataRequest request, CancellationToken cancellationToken) =>
{
    await using var stream = request.File.OpenReadStream();
    var metadata = string.IsNullOrWhiteSpace(request.JsonMetadata) ? null : JsonSerializer.Deserialize<Dictionary<string, string?>>(request.JsonMetadata, JsonSerializerOptions.Web);

    await storageProvider.SaveAsync(Path.Combine(request.Folder ?? string.Empty, request.File.FileName), stream, metadata, request.Overwrite, cancellationToken);

    return TypedResults.NoContent();
})
.DisableAntiforgery();

attachmentsApiGroup.MapPut("metadata", async (IStorageProvider storageProvider, string fileName, IDictionary<string, string?>? metadata = null, string? folder = null, CancellationToken cancellationToken = default) =>
{
    await storageProvider.SetMetadataAsync(Path.Combine(folder ?? string.Empty, fileName), metadata, cancellationToken);
    return TypedResults.NoContent();
});

attachmentsApiGroup.MapGet("content", async Task<Results<FileStreamHttpResult, NotFound>> (IStorageProvider storageProvider, string fileName, CancellationToken cancellationToken) =>
{
    var attachment = await storageProvider.ReadAsStreamAsync(fileName, cancellationToken);

    return attachment is null ? TypedResults.NotFound() : TypedResults.Stream(attachment, MimeUtility.GetMimeMapping(fileName));
});

attachmentsApiGroup.MapDelete(string.Empty, async (IStorageProvider storageProvider, string fileName, CancellationToken cancellationToken) =>
{
    await storageProvider.DeleteAsync(fileName, cancellationToken);
    return TypedResults.NoContent();
});

app.Run();
