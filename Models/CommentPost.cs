
namespace ForumDataMigration.Models;

public class CommentPost : Lctech.Comment.Domain.Entities.Comment
{
    public int Tid { get; set; }
    public int Fid { get; set; }
    public int Pid { get; set; }
    public int Authorid { get; set; }
    public uint Dateline { get; set; }
    public bool First { get; set; }
    public ushort PostStatus { get; set; }
    public uint? StickDateline { get; set; }
    public bool Comment { get; set; }
    public bool Invisible { get; set; }
    
    public uint Replies { get; set; }
}