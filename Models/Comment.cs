using Lctech.Jkf.Domain.Enums;
using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class Comment : Entity
{
    public long RootId { get; set; }
    public long? ParentId { get; set; }
    public int Level { get; set; }
    public string Hierarchy { get; set; } = default!;
    public string? Title { get; set; }
    public string Content { get; set; } = default!;
    public VisibleType VisibleType { get; set; }
    public string? Ip { get; set; }
    public long Sequence { get; set; }
    public long SortingIndex { get; set; }
    public long RelatedScore { get; set; }
    public long ReplyCount { get; set; }
    public long LikeCount { get; set; }
    public long DislikeCount { get; set; }
    public bool IsDeleted { get; set; }
}