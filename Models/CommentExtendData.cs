using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class CommentExtendData : Entity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}