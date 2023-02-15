namespace ForumDataMigration.Models;

public class CommentPostResult
{
    public long ArticleId { get; set; }
    public long BoardId { get; set; }
    public long MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public DateTimeOffset CreateDate { get; set; }
    public long CreateMilliseconds { get; set; }
    public CommentPost Post { get; set; } = default!;
    public Dictionary<int, List<Attachment>> AttachmentDic { get; set; } = new();
    public int ReplyMemberUid { get; set; } = default!;
    public string ReplyMemberName { get; set; } = default!;
}