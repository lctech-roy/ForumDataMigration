namespace ForumDataMigration.Models;

public class PollVoter
{
    [Slapper.AutoMapper.Id]
    public uint Tid { get; set; }
    [Slapper.AutoMapper.Id]
    public uint Uid { get; set; }
    public string Options { get; set; } = null!;
    public uint Dateline { get; set; }
}