using MimeMapping;

namespace StorageProviders;

public class StorageFileInfo
{
    public string Name { get; }

    public string ContentType { get; }

    public DateTimeOffset LastModified { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long Length { get; set; }

    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    public StorageFileInfo(string name)
    {
        Name = name;
        ContentType = MimeUtility.GetMimeMapping(name);
    }
}
