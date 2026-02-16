using System.Text.Json;
using AzureStorageSample.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MimeMapping;
using StorageProviders;
using StorageProviders.AzureStorage;
using TinyHelpers.AspNetCore.Extensions;
using TinyHelpers.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    options.AddDefaultProblemDetailsResponse();
});

builder.Services.AddAzureStorage(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("AzureStorageConnection")!;
    options.ContainerName = builder.Configuration.GetValue<string>("AppSettings:ContainerName");
});

builder.Services.AddDefaultProblemDetails();
builder.Services.AddDefaultExceptionHandler();

var app = builder.Build();
app.UseHttpsRedirection();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", app.Environment.ApplicationName);
    options.RoutePrefix = string.Empty;
});

var attachementsApiGroup = app.MapGroup("/api/attachments");

attachementsApiGroup.MapGet(string.Empty, (IStorageProvider storageProvider, string? prefix = null, [FromQuery(Name = "extension")] string[] extensions = null!) =>
{
    var attachments = storageProvider.EnumerateAsync(prefix, extensions);

    // If you need to get the actual list, you can use the .ToListAsync() extension method:
    //var list = await attachments.ToListAsync();

    return TypedResults.Ok(attachments);
});

attachementsApiGroup.MapGet("exists", async Task<Results<NoContent, NotFound>> (IStorageProvider storageProvider, string fileName) =>
{
    var exists = await storageProvider.ExistsAsync(fileName);
    if (!exists)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.NoContent();
});

attachementsApiGroup.MapGet("full-path", async (IStorageProvider storageProvider, string fileName) =>
{
    var fullPath = await storageProvider.GetFullPathAsync(fileName);
    return TypedResults.Ok(fullPath);
});

attachementsApiGroup.MapGet("info", async (IStorageProvider storageProvider, string fileName) =>
{
    var fullPath = await storageProvider.GetPropertiesAsync(fileName);
    return TypedResults.Ok(fullPath);
});

attachementsApiGroup.MapGet("read-uri", async (IStorageProvider storageProvider, string fileName, DateTime expirationDate) =>
{
    var readUri = await storageProvider.GetReadAccessUriAsync(fileName, expirationDate);
    return TypedResults.Ok(readUri);
});

attachementsApiGroup.MapPost(string.Empty, async (IFormFile file, IStorageProvider storageProvider, string? folder = null, bool overwrite = false) =>
{
    using var stream = file.OpenReadStream();
    await storageProvider.SaveAsync(Path.Combine(folder ?? string.Empty, file.FileName), stream, overwrite);

    return TypedResults.NoContent();
})
.DisableAntiforgery();

attachementsApiGroup.MapPost("upload-metadata", async Task<Results<NoContent, BadRequest<string>>> (IStorageProvider storageProvider, [FromForm] UploadFileWithMetadataRequest request, CancellationToken cancellationToken) =>
{
    using var stream = request.File.OpenReadStream();
    
    Dictionary<string, string>? metadata = null;
    if (!string.IsNullOrWhiteSpace(request.JsonMetadata))
    {
        try
        {
            metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(request.JsonMetadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return TypedResults.BadRequest("The JsonMetadata field contains invalid JSON.");
        }
    }

    await storageProvider.SaveAsync(Path.Combine(request.Folder ?? string.Empty, request.File.FileName), stream, metadata, request.Overwrite, cancellationToken);

    return TypedResults.NoContent();
})
.DisableAntiforgery();

attachementsApiGroup.MapPut("metadata", async Task<Results<NoContent, NotFound>> (IStorageProvider storageProvider, string fileName, IDictionary<string, string>? metadata = null, string? folder = null) =>
{
    var success = await storageProvider.SetMetadataAsync(Path.Combine(folder ?? string.Empty, fileName), metadata);
    if (!success)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.NoContent();
})
.DisableAntiforgery();

attachementsApiGroup.MapGet("content", async Task<Results<FileStreamHttpResult, NotFound>> (IStorageProvider storageProvider, string fileName) =>
{
    var attachment = await storageProvider.ReadAsStreamAsync(fileName);
    if (attachment is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Stream(attachment, MimeUtility.GetMimeMapping(fileName));
});

attachementsApiGroup.MapDelete(string.Empty, async (IStorageProvider storageProvider, string fileName) =>
{
    await storageProvider.DeleteAsync(fileName);
    return TypedResults.NoContent();
});

app.Run();