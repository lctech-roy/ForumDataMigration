using System.Reflection.Metadata.Ecma335;

namespace ForumDataMigration.Models;

public class PostResult
{
    public long ArticleId { get; set; }
    public long BoardId { get; set; }
    public long MemberId { get; set; }
    public DateTimeOffset CreateDate { get; set; }
    public long CreateMilliseconds { get; set; }
    public int HideExpirationDay { get; set; }
    public int RewardExpirationDay { get; set; }
    
    public Dictionary<int, long> CategoryDic {get; set; } = default!;
    public Dictionary<(int, string), int> ModDic { get; set; } = default!;
    public Dictionary<long, Read> ReadDic { get; set; } = default!;
    public Dictionary<string, long> MemberDisplayNameDic { get; set; } = default!;
    public Dictionary<int, long> MemberUidDic { get; set; } = default!;
    public Post Post { get; set; } = default!;
}