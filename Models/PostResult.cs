using System.Reflection.Metadata.Ecma335;

namespace ForumDataMigration.Models;

public class PostResult
{
    public long ArticleId { get; set; }
    public long BoardId { get; set; }
    public long MemberId { get; set; }
    public string MemberName { get; set; } = default!;
    public int ReplyMemberUid { get; set; } = default!;
    public string ReplyMemberName { get; set; } = default!;
    public long? LastPosterId { get; set; }
    public DateTimeOffset CreateDate { get; set; }
    public long CreateMilliseconds { get; set; }
    public Post Post { get; set; } = default!;
    public Dictionary<int, string> AttachPathDic { get; set; } = new();
}