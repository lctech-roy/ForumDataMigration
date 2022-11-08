using Lctech.Jkf.Forum.Enums;
using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class Attachment : Entity
{
    public long? Size { get; set; }
    public int DownloadCount { get; set; }
    public string ExternalLink { get; set; } = default!;
    public int DeleteStatus { get; set; } = 0;
    public int ProcessingState { get; set; } = 0;
    //domain額外資訊
    public int TableId { get; set; }
    public int Aid { get; set; }
    public bool Remote { get; set; }
    public bool IsImage { get; set; } 
    public uint Dateline { get; set; }
    public string BbCode { get; set; } = default!;
}