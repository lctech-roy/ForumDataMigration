namespace ForumDataMigration.Models;

public class ArticleCoverRelation
{
    public long Id { get; set; }
    public string OriginCover { get; set; } = default!;
    public int Tid { get; set; }
    public int Pid { get; set; }
    public string? AttachmentUrl { get; set; }
}