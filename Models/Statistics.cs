namespace ForumDataMigration.Models;

public class Statistics
{
    [Slapper.AutoMapper.Id]
    public DateOnly Logdate { get; set; }
    [Slapper.AutoMapper.Id]
    public uint Fid { get; set; }
    //public ushort Type { get; set; } //type都是1發文數
    public uint Value { get; set; }
}