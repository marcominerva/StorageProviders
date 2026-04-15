# Storage Providers

[![Lint Code Base](https://github.com/marcominerva/StorageProviders/actions/workflows/linter.yml/badge.svg)](https://github.com/marcominerva/StorageProviders/actions/workflows/linter.yml)
[![CodeQL](https://github.com/marcominerva/StorageProviders/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/marcominerva/StorageProviders/actions/workflows/github-code-scanning/codeql)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/marcominerva/StorageProviders/blob/master/LICENSE)


A collection of Storage Providers for various destinations.

## Azure Storage

[![NuGet](https://img.shields.io/nuget/v/StorageProviders.AzureStorage.svg?style=flat-square)](https://www.nuget.org/packages/StorageProviders.AzureStorage)
[![Nuget](https://img.shields.io/nuget/dt/StorageProviders.AzureStorage)](https://www.nuget.org/packages/StorageProviders.AzureStorage)

**Installation**

The library is available on [NuGet](https://www.nuget.org/packages/StorageProviders.AzureStorage). Search for *StorageProviders.AzureStorage* in the **Package Manager GUI** or run the following command in the **.NET CLI**:

```bash
dotnet add package StorageProviders.AzureStorage
```

## How the library works

The package exposes the `IStorageProvider` abstraction, which offers a single asynchronous API for common file storage operations:

- save content from `byte[]` or `Stream`
- read content as `Stream` or `byte[]`
- verify whether a file exists
- enumerate files by prefix and extension
- delete files
- read file properties and metadata
- update metadata
- build the full file URI
- generate a temporary read-only URI

The abstraction is designed so application code depends only on `IStorageProvider`, while the concrete provider can be registered through dependency injection.

## `IStorageProvider` overview

The interface contains convenience overloads and stream-based methods:

- `SaveAsync` supports uploads from both `byte[]` and `Stream`
- `ReadAsStreamAsync` returns the file content as a stream
- `ReadAsByteArrayAsync` is a convenience wrapper built on top of `ReadAsStreamAsync`
- `GetReadAccessUriAsync` can optionally set a download file name when the provider supports it
- `EnumerateAsync` returns an `IAsyncEnumerable<string>` so files can be streamed progressively
- `SetMetadataAsync` updates the metadata associated with a file

Because the API is fully asynchronous, it works well in ASP.NET Core, background services, and other I/O-bound workloads.

## Registering Azure Storage

The Azure implementation is provided by `StorageProviders.AzureStorage` and can be registered in two ways.

### Static configuration

Use this overload when the Azure settings are known at startup:

```csharp
builder.Services.AddAzureStorage(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("AzureStorageConnection")!;
    options.ContainerName = builder.Configuration.GetValue<string>("AppSettings:ContainerName");
});
```

This registration adds:

- `AzureStorageSettings` as a singleton
- `IStorageProvider` mapped to `AzureStorageProvider` as a singleton

### Configuration resolved from the service provider

Use this overload when the storage settings depend on other registered services:

```csharp
builder.Services.AddAzureStorage((serviceProvider, options) =>
{
    options.ConnectionString = serviceProvider
        .GetRequiredService<IConfiguration>()
        .GetConnectionString("AzureStorageConnection")!;

    options.ContainerName = serviceProvider
        .GetRequiredService<IConfiguration>()
        .GetValue<string>("AppSettings:ContainerName");
});
```

This registration adds:

- `AzureStorageSettings` as scoped
- `IStorageProvider` mapped to `AzureStorageProvider` as scoped

## Azure provider behavior

`AzureStorageProvider` uses Azure Blob Storage and requires:

- `ConnectionString`: the Azure Storage connection string
- `ContainerName`: optional default container name

If `ContainerName` is configured, paths are treated as blob paths inside that container:

```text
documents/report.pdf
images/logo.png
```

If `ContainerName` is not configured, the first segment of the path is interpreted as the container name:

```text
documents/report.pdf   -> container: documents, blob: report.pdf
images/logo.png        -> container: images, blob: logo.png
```

Backslashes are normalized to forward slashes, so Windows-style paths are also accepted.

## Main operations

### Upload a file

```csharp
using var stream = file.OpenReadStream();
await storageProvider.SaveAsync(file.FileName, stream, overwrite: false);
```

You can also upload metadata:

```csharp
var metadata = new Dictionary<string, string>
{
    ["category"] = "invoice",
    ["customerId"] = "42"
};

using var stream = file.OpenReadStream();
await storageProvider.SaveAsync(file.FileName, stream, metadata, overwrite: true);
```

When `overwrite` is `false`, the Azure provider throws an `IOException` if the blob already exists.

### Read a file

```csharp
await using var stream = await storageProvider.ReadAsStreamAsync("documents/report.pdf");
```

Or read it as a byte array:

```csharp
var content = await storageProvider.ReadAsByteArrayAsync("documents/report.pdf");
```

### Check whether a file exists

```csharp
var exists = await storageProvider.ExistsAsync("documents/report.pdf");
```

### Enumerate files

```csharp
await foreach (var path in storageProvider.EnumerateAsync("documents", [".pdf", ".docx"]))
{
    Console.WriteLine(path);
}
```

This method supports:

- an optional prefix
- filtering by extension
- asynchronous streaming of results

### Get file information

```csharp
var fileInfo = await storageProvider.GetPropertiesAsync("documents/report.pdf");
```

The returned `StorageFileInfo` contains:

- file name
- inferred content type
- size
- creation date
- last modification date
- metadata

### Update metadata

```csharp
await storageProvider.SetMetadataAsync("documents/report.pdf", new Dictionary<string, string>
{
    ["category"] = "archived"
});
```

Passing `null` clears the existing metadata for the file.

### Get the full blob URI

```csharp
var uri = await storageProvider.GetFullPathAsync("documents/report.pdf");
```

### Generate a temporary read URI

```csharp
var uri = await storageProvider.GetReadAccessUriAsync(
    "documents/report.pdf",
    expirationDate: DateTime.UtcNow.AddMinutes(30),
    fileName: "Report.pdf");
```

For Azure Blob Storage this produces a SAS URI with read permissions. If a file name is provided, the provider also sets the `Content-Disposition` header so the browser can suggest a download name.

### Delete a file

```csharp
await storageProvider.DeleteAsync("documents/report.pdf");
```

## Example with ASP.NET Core Minimal APIs

The sample project in `samples/AzureStorageSample` shows how to inject `IStorageProvider` in endpoints and use it for:

- upload
- upload with metadata
- file existence checks
- file listing
- file download
- metadata updates
- file deletion
- generating full and temporary read URIs

Example registration:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureStorage(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("AzureStorageConnection")!;
    options.ContainerName = builder.Configuration.GetValue<string>("AppSettings:ContainerName");
});
```

Example endpoint:

```csharp
app.MapGet("/api/attachments/full-path", async (IStorageProvider storageProvider, string fileName) =>
{
    var fullPath = await storageProvider.GetFullPathAsync(fileName);
    return Results.Ok(fullPath);
});
```

## Notes

- The Azure provider automatically creates the target container when saving a file, if it does not exist.
- Uploaded blobs use a content type inferred from the file name.
- `ReadAsStreamAsync` and `SetMetadataAsync` throw when the target blob does not exist.
- The API is storage-oriented and does not depend on ASP.NET Core, so it can also be used in console apps, workers, and class libraries.

**Contribute**

The project is constantly evolving. Contributions are welcome. Feel free to file issues and pull requests in the repository, and we'll address them as we can. 
