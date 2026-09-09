using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StorageProviders.FileSystem;

internal sealed class FileSystemStorageProvider : IStorageProvider
{
    private const string ProviderDirectoryName = ".storageproviders";
    private const string MetadataDirectoryName = "metadata";

    private readonly string rootDirectory;
    private readonly string metadataDirectory;

    public FileSystemStorageProvider(FileSystemStorageSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RootDirectory))
        {
            throw new ArgumentException("The root directory must be configured.", nameof(settings));
        }

        rootDirectory = Path.GetFullPath(settings.RootDirectory);
        metadataDirectory = Path.Combine(rootDirectory, ProviderDirectoryName, MetadataDirectoryName);

        Directory.CreateDirectory(rootDirectory);
    }

    public async Task SaveAsync(string path, Stream stream, IDictionary<string, string?>? metadata, bool overwrite, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;

        Directory.CreateDirectory(directory);
        EnsureNoReparsePoints(fullPath);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        try
        {
            var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;
            await using var fileStream = new FileStream(fullPath, fileMode, FileAccess.Write, FileShare.None, bufferSize: 81920, FileOptions.Asynchronous);
            await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (!overwrite && File.Exists(fullPath))
        {
            throw new IOException($"The file {path} already exists.");
        }

        await WriteMetadataAsync(path, metadata, cancellationToken).ConfigureAwait(false);
    }

    public Task<Stream?> ReadAsStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetExistingFilePath(path);

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(path);
        EnsureNoReparsePoints(fullPath);

        return Task.FromResult(File.Exists(fullPath));
    }

    public async IAsyncEnumerable<string> EnumerateAsync(string? prefix, IEnumerable<string> extensions, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? null : NormalizeLogicalPath(prefix);
        if (normalizedPrefix is not null)
        {
            EnsureValidFullPath(normalizedPrefix);
        }

        var extensionSet = extensions?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var enumerationOptions = new EnumerationOptions
        {
            IgnoreInaccessible = false,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var filePath in Directory.EnumerateFiles(rootDirectory, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var logicalPath = Path.GetRelativePath(rootDirectory, filePath).Replace(Path.DirectorySeparatorChar, '/');
            if (logicalPath.StartsWith($"{ProviderDirectoryName}/", StringComparison.OrdinalIgnoreCase) || (normalizedPrefix is not null && !logicalPath.StartsWith(normalizedPrefix, GetPathComparison()))
                || (extensionSet.Count > 0 && !extensionSet.Contains(Path.GetExtension(filePath))))
            {
                continue;
            }

            yield return logicalPath;
            await Task.Yield();
        }
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(path);
        EnsureNoReparsePoints(fullPath);

        File.Delete(fullPath);
        DeleteMetadata(path);

        return Task.CompletedTask;
    }

    public async Task<StorageFileInfo> GetPropertiesAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetExistingFilePath(path);
        var info = new FileInfo(fullPath);
        var metadata = await ReadMetadataAsync(path, cancellationToken).ConfigureAwait(false);

        return new StorageFileInfo(NormalizeLogicalPath(path))
        {
            Length = info.Length,
            CreatedOn = info.CreationTimeUtc,
            LastModified = info.LastWriteTimeUtc,
            Metadata = metadata
        };
    }

    public Task<Uri> GetFullPathAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = GetFullPath(path);
        EnsureNoReparsePoints(fullPath);

        return Task.FromResult(new Uri(fullPath));
    }

    public Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, string? fileName = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureValidFullPath(path);

        return Task.FromResult<Uri?>(null);
    }

    public Task SetMetadataAsync(string path, IDictionary<string, string?>? metadata = null, CancellationToken cancellationToken = default)
    {
        EnsureExistingFilePath(path);
        return WriteMetadataAsync(path, metadata, cancellationToken);
    }

    private string GetExistingFilePath(string path)
    {
        EnsureExistingFilePath(path);
        return ResolveFullPath(path);
    }

    private string GetFullPath(string path)
    {
        EnsureValidFullPath(path);
        return ResolveFullPath(path);
    }

    private void EnsureExistingFilePath(string path)
    {
        EnsureValidFullPath(path);

        var fullPath = ResolveFullPath(path);
        EnsureNoReparsePoints(fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"The file {path} does not exist.", fullPath);
        }
    }

    private void EnsureValidFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            throw new ArgumentException($"The path '{path}' must be relative to the configured root directory.", nameof(path));
        }

        var normalizedPath = NormalizeLogicalPath(path);
        var firstSegment = normalizedPath.Split('/', 2)[0];
        if (string.Equals(firstSegment, ProviderDirectoryName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The path '{path}' is reserved for provider data.", nameof(path));
        }

        var fullPath = ResolveFullPath(path);
        var rootDirectoryPrefix = Path.EndsInDirectorySeparator(rootDirectory) ? rootDirectory : rootDirectory + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootDirectoryPrefix, GetPathComparison()))
        {
            throw new ArgumentException($"The path '{path}' resolves outside the configured root directory.", nameof(path));
        }
    }

    private string ResolveFullPath(string path) => Path.GetFullPath(NormalizeLogicalPath(path), rootDirectory);

    private void EnsureNoReparsePoints(string fullPath)
    {
        // A path can remain lexically inside the storage root while a symbolic link redirects
        // file-system access outside it, so every existing segment must be checked.
        var currentPath = fullPath;
        while (!string.Equals(currentPath, rootDirectory, GetPathComparison()))
        {
            if ((File.Exists(currentPath) || Directory.Exists(currentPath)) && (File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Symbolic links and other reparse points are not supported: {currentPath}");
            }

            // Walk upward until the trusted root is reached, including both the target and its parents.
            currentPath = Path.GetDirectoryName(currentPath)!;
        }
    }

    private async Task<IDictionary<string, string?>> ReadMetadataAsync(string path, CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath(path);
        if (!File.Exists(metadataPath))
        {
            return new FileSystemMetadata();

        }

        await using var stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var metadata = await JsonSerializer.DeserializeAsync(stream, FileSystemJsonSerializerContext.Default.FileSystemMetadata, cancellationToken).ConfigureAwait(false) ?? [];

        return metadata;
    }

    private async Task WriteMetadataAsync(string path, IDictionary<string, string?>? metadata, CancellationToken cancellationToken)
    {
        var metadataPath = GetMetadataPath(path);
        if (metadata is null || metadata.Count == 0)
        {
            DeleteMetadata(path);
            return;
        }

        Directory.CreateDirectory(metadataDirectory);

        await using var stream = new FileStream(metadataPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, new FileSystemMetadata(metadata), FileSystemJsonSerializerContext.Default.FileSystemMetadata, cancellationToken).ConfigureAwait(false);
    }

    private void DeleteMetadata(string path)
    {
        if (Directory.Exists(metadataDirectory))
        {
            File.Delete(GetMetadataPath(path));
        }
    }

    private string GetMetadataPath(string path)
    {
        var normalizedPath = NormalizeLogicalPath(path);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));

        return Path.Combine(metadataDirectory, $"{hash}.json");
    }

    private static string NormalizeLogicalPath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static StringComparison GetPathComparison()
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
