using Lctech.Jkf.Forum.Domain.Entities;

namespace ForumDataMigration.Models;

public class BlackListMember : ArticleBlackListMember
{
    public string BlackUserNames { get; set; } = default!;
}