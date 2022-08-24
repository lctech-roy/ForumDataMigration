using System.Reflection.Metadata.Ecma335;

namespace ForumDataMigration.Models;

public class PostResult
{
    public long ArticleId { get; set; }
    public long BoardId { get; set; }
    public long MemberId { get; set; }
    public DateTimeOffset CreateDate { get; set; }
    public long CreateMilliseconds { get; set; }
    public Dictionary<string, long> MemberDisplayNameDic { get; set; } = default!;
    public Dictionary<int, long> MemberUidDic { get; set; } = default!;
    public Post Post { get; set; } = default!;
}