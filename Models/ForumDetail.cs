namespace ForumDataMigration.Models;

public class ForumDetail
{
    public long Fid { get; set; }
    public string Icon { get; set; } = default!;
    public string Moderators { get; set; } = default!;
}