using ForumDataMigration.Enums;
using Lctech.Jkf.Forum.Enums;

namespace ForumDataMigration.Models;

public class Document
{
    public long Id { get; set; }
    public DocumentType Type { get; set; }

    public string? Title { get; set; }
    public string Content { get; set; } = null!;
    public long ReadPermission { get; set; }
    public string? Tag { get; set; }
    public long? ThumbnailId { get; set; }
    public long? BoardId { get; set; }
    public long? CategoryId { get; set; }
    public long? RootId { get; set; }
    public long? ParentId { get; set; }
    public int Sequence { get; set; }
    public long SortingIndex { get; set; }
    public decimal Score { get; set; }
    public string? Ip { get; set; }
    public PinType PinType { get; set; }
    public int PinPriority { get; set; }
    public VisibleType VisibleType { get; set; }
    public ArticleStatus Status { get; set; }

    public DateTimeOffset? LastReplyDate { get; set; }
    public long? LastReplierId { get; set; }
    public long? LastReplierUid { get; set; }
    public string? LastReplierName { get; set; }

    public DateTimeOffset CreationDate { get; set; }
    public long CreatorId { get; set; }
    public long? CreatorUid { get; set; }
    public string CreatorName { get; set; } = null!;

    public DateTimeOffset ModificationDate { get; set; }
    public long ModifierId { get; set; }
    public long? ModifierUid { get; set; }
    public string ModifierName { get; set; } = null!;

    public DeleteStatus DeleteStatus { get; set; }
    public Relationship Relationship { get; set; } = null!;
}

public class Relationship
{
    public string? Name { get; set; }
    public string? Parent { get; set; }
}