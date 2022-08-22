using System.Text.Json.Serialization;
using Lctech.Jkf.Domain.Entities;

namespace ForumDataMigration.Models;

public class ArticleRatingJson : ArticleRating
{
    [JsonIgnore]
    public new long ArticleId{ get; set; }
    [JsonIgnore]
    public override long Version{ get; set; }
    [JsonIgnore]
    public override  ICollection<ArticleRatingItem> Items{ get; set; } = null!;
    public long RootId{ get; set; }
    public long BoardId { get; set; }
    public long CategoryId { get; set; }
    public long CreatorUid { get; set; }
    public string CreatorName { get; set; } = null!;
    public long? ModifierUid { get; set; }
    public string ModifierName { get; set; } = null!;
    
    public short Type { get; set; } = 2;
    public bool Deleted { get; set; } = false;
}