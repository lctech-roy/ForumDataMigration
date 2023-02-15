namespace ForumDataMigration.Models;

public class ArticlePostResult
{
    public long ArticleId { get; set; }
    public long BoardId { get; set; }
    public long MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public DateTimeOffset CreateDate { get; set; }
    public long CreateMilliseconds { get; set; }
    public ArticlePost Post { get; set; } = default!;
    public Dictionary<int, List<Attachment>> AttachmentDic { get; set; } = new();
}