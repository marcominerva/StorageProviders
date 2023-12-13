using MimeMapping;

namespace StorageProviders;

public class StorageFileInfo(string name)
{
    public string Name { get; } = name;

    public string ContentType { get; } = MimeUtility.GetMimeMapping(name);

    public DateTimeOffset LastModified { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long Length { get; set; }

    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
