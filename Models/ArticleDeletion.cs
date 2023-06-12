namespace ForumDataMigration.Models;

public class ArticleDeletion
{
    public long Id { get; set; }
    public long DeleterId { get; set; }
    public long DeletionDateInt { get; set; }
    public DateTimeOffset DeletionDate { get; set; }
    public string DeletionReason { get; set; } = default!;
}