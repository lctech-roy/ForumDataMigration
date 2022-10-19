using Lctech.Jkf.Forum.Enums;
using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class Attachment : Entity
{
    public string Name { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileExtension { get; set; } = string.Empty!;
    public string StoragePath { get; set; } = null!;
    public int DownloadCount { get; set; }
    public bool IncludeFile { get; set; }
    public bool IsExternal { get; set; } = true;
    public DeleteStatus DeleteStatus { get; set; }
}