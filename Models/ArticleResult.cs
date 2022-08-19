using Lctech.Jkf.Domain.Entities;

namespace ForumDataMigration.Models;

public class ArticleResult
{
    public Article Article { get; set; } = default!;
    public Comment Comment { get; set; } = default!;
    public Warning? Warning { get; set; }
}