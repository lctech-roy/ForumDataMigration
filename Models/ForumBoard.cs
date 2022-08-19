namespace ForumDataMigration.Models;

public class ForumBoard
{
    public long Fid { get; set; }
    public long Fup { get; set; }
    public BoardType Type { get; set; }
    public string Name { get; set; } = default!;
    public int Status { get; set; }
    public long Posts { get; set; }
    public long Favtimes { get; set; }
}

public enum BoardType { Group = 1, Forum = 2, Sub = 3, }