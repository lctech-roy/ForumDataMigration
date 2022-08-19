namespace ForumDataMigration.Models;

public class PollOption
{
    public uint Tid { get; set; }
    [Slapper.AutoMapper.Id]
    public uint Polloptionid { get; set; }
    public uint Votes { get; set; }
    public string? Polloption { get; set; }
}