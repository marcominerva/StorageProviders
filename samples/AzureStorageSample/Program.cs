using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using MimeMapping;
using MinimalHelpers.OpenApi;
using StorageProviders;
using StorageProviders.AzureStorage;
using TinyHelpers.AspNetCore.Extensions;
using TinyHelpers.AspNetCore.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Azure Storage API", Version = "v1" });

    options.AddFormFile();
    options.AddDefaultResponse();
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

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Azure Storage API v1");
    options.RoutePrefix = string.Empty;
});

var attachementsApiGroup = app.MapGroup("/api/attachments");

attachementsApiGroup.MapGet(string.Empty, (IStorageProvider storageProvider, string? prefix = null, [FromQuery(Name = "extension")] string[] extensions = null!) =>
{
    var attachments = storageProvider.EnumerateAsync(prefix, extensions);

    // If you need to get the actual list, you can use the .ToListAsync() extension method:
    //var list = await attachments.ToListAsync();

    return TypedResults.Ok(attachments);
})
.WithOpenApi();

attachementsApiGroup.MapGet("exists", async Task<Results<NoContent, NotFound>> (IStorageProvider storageProvider, string fileName) =>
{
    var exists = await storageProvider.ExistsAsync(fileName);
    if (!exists)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.NoContent();
})
.WithOpenApi();

attachementsApiGroup.MapGet("full-path", async (IStorageProvider storageProvider, string fileName) =>
{
    var fullPath = await storageProvider.GetFullPathAsync(fileName);
    return TypedResults.Ok(fullPath);
});

attachementsApiGroup.MapGet("info", async (IStorageProvider storageProvider, string fileName) =>
{
    var fullPath = await storageProvider.GetPropertiesAsync(fileName);
    return TypedResults.Ok(fullPath);
})
.WithOpenApi();

attachementsApiGroup.MapGet("read-uri", async (IStorageProvider storageProvider, string fileName, DateTime expirationDate) =>
{
    var readUri = await storageProvider.GetReadAccessUriAsync(fileName, expirationDate);
    return TypedResults.Ok(readUri);
})
.WithOpenApi();

attachementsApiGroup.MapPost(string.Empty, async (IFormFile file, IStorageProvider storageProvider, string? folder = null, bool overwrite = false) =>
{
    using var stream = file.OpenReadStream();
    await storageProvider.SaveAsync(Path.Combine(folder ?? string.Empty, file.FileName), stream, overwrite);

    return TypedResults.NoContent();
})
.DisableAntiforgery()
.WithOpenApi();

attachementsApiGroup.MapGet("content", async Task<Results<FileStreamHttpResult, NotFound>> (IStorageProvider storageProvider, string fileName) =>
{
    var attachment = await storageProvider.ReadAsStreamAsync(fileName);
    if (attachment is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Stream(attachment, MimeUtility.GetMimeMapping(fileName));
})
.WithOpenApi();

attachementsApiGroup.MapDelete(string.Empty, async (IStorageProvider storageProvider, string fileName) =>
{
    await storageProvider.DeleteAsync(fileName);
    return TypedResults.NoContent();
})
.WithOpenApi();

app.Run();