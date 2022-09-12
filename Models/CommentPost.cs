namespace ForumDataMigration.Models;

public class CommentPost : Comment
{
    public int Tid { get; set; }
    public int Fid { get; set; }
    public uint Pid { get; set; }
    public int Authorid { get; set; }
    public uint Dateline { get; set; }
    public bool First { get; set; }
    public ushort Status { get; set; }
    public uint? StickDateline { get; set; }
    public bool Comment { get; set; }
}