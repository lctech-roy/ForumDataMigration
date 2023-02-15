namespace ForumDataMigration.Models;

public class ThreadWarning
{
    [Slapper.AutoMapper.Id]
    public uint Pid { get; set; }

    public uint Operatorid { get; set; }
    public uint Authorid { get; set; }
    public uint Dateline { get; set; }
    public string Reason { get; set; } = null!;

    // public Thread Thread { get; set; } = default!;
}