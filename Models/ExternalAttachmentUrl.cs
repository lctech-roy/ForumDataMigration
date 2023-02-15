namespace ForumDataMigration.Models;

public class ExternalAttachmentUrl
{
    public long AttachmentId { get; set; }
    public int Tid { get; set; }
    public int Pid { get; set; }
    public string? AttachmentUrl { get; set; }
}