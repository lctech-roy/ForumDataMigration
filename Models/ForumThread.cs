namespace ForumDataMigration.Models;

public class ForumThread
{
    [Slapper.AutoMapper.Id]
    public uint Tid { get; set; }
    public uint Pid { get; set; }
    public ushort Typeid { get; set; }
    public byte Readperm { get; set; }
    public uint Closed { get; set; }
    public short Displayorder { get; set; }
    public short Special { get; set; }
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public short Highlight { get; set; }
    public bool Digest { get; set; }
    public uint Views { get; set; }
    public uint Replies { get; set; }
    public uint Sharetimes { get; set; }
    public int? ThankCount { get; set; }
    public uint? Ratetimes { get; set; }
    public uint? Lastpost { get; set; }
    public uint? PostTime { get; set; }
    public uint Authorid { get; set; }
    public uint Dateline { get; set; }
    public string Useip { get; set; } = null!;
    public short Price { get; set; }
    public int ReadFloor { get; set; }
    public int ReadUid { get; set; }
    public bool Usesig { get; set; }

    public ThreadWarning? Warning { get; set; }
    public Poll? Poll { get; set; }
    
    public virtual ICollection<RateLog> RateLogs { get; set; } = new HashSet<RateLog>();
}