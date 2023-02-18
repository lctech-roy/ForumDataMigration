namespace ForumDataMigration.Models;

public class CommentPost
{
    public int Tid { get; set; }
    public int Fid { get; set; }
    public int Pid { get; set; }
    public int Authorid { get; set; }
    public uint Dateline { get; set; }
    public bool First { get; set; }
    public ushort Status { get; set; }
    public uint? StickDateline { get; set; }
    public bool Comment { get; set; }
    public bool Invisible { get; set; }
    public string Content { get; set; } = string.Empty;
    public uint Replies { get; set; }
    public int Sequence { get; set; }
    
    //擴充欄位
    public DateTimeOffset CreateDate { get; set; }
    public long CreateMilliseconds { get; set; }
}