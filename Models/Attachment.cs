using Lctech.Jkf.Forum.Enums;
using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class Attachment : Entity
{
    public long? Size { get; set; }
    public string ExternalLink { get; set; } = default!;
    public int DownloadCount { get; set; }
    public int ProcessingState { get; set; } = 0;
    public int DeleteStatus { get; set; } = 0;
    public string? StoragePath { get; set; }
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public long? ParentId { get; set; }
    public string? Bucket { get; set; }
    public bool IsPublish { get; set; }
    //for artifact
    public string ObjectName { get; set; } = string.Empty;
    
    //domain額外資訊
    public int TableId { get; set; }
    public int Aid { get; set; }
    public bool Remote { get; set; }
    public bool IsImage { get; set; } 
    public uint Dateline { get; set; }
    public string BbCode { get; set; } = default!;
}