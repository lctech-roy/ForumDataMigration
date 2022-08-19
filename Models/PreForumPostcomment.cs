namespace ForumDataMigration.Models;

public class PreForumPostcomment
{
    public uint Id { get; set; }
    public uint Tid { get; set; }
    public uint Pid { get; set; }
    public string? Author { get; set; }
    public int Authorid { get; set; }
    public uint Dateline { get; set; }
    public string Comment { get; set; } = null!;
    public bool Score { get; set; }
    public string Useip { get; set; } = null!;
    public uint Rpid { get; set; }
}