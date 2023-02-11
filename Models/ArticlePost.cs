namespace ForumDataMigration.Models;

public class ArticlePost
{
    //[Slapper.AutoMapper.Id]
    public int Tid { get; set; }
    public int Fid { get; set; }
    public ushort Typeid { get; set; }
    public byte Readperm { get; set; }
    public uint Closed { get; set; }
    public ushort Status { get; set; }
    public short Displayorder { get; set; }
    public short Special { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Thumb { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public short Highlight { get; set; }
    public bool Digest { get; set; }
    public uint Views { get; set; }
    public uint Replies { get; set; }
    public uint Sharetimes { get; set; }
    public int? ThankCount { get; set; }
    public uint? Ratetimes { get; set; } = 0;
    public uint? Lastpost { get; set; }
    public int? Lastposter { get; set; }
    public uint? PostTime { get; set; }
    public int Authorid { get; set; }
    public uint Dateline { get; set; }
    public string Useip { get; set; } = null!;
    public short Price { get; set; }
    public bool Usesig { get; set; }
    public uint? Sexpiry { get; set; }
    public uint? Hexpiry { get; set; }
}