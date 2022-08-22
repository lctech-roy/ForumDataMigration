namespace ForumDataMigration.Models;

public class RateLog
{
    [Slapper.AutoMapper.Id]
    public uint Pid { get; set; }
    [Slapper.AutoMapper.Id]
    public int Tid { get; set; }
    [Slapper.AutoMapper.Id]
    public int Uid { get; set; }
    [Slapper.AutoMapper.Id]
    public byte Extcredits { get; set; }
    public int Fid { get; set; }
    public ushort Typeid { get; set; }
    public uint Dateline { get; set; }
    public short Score { get; set; }
    public string Reason { get; set; } = null!;
    public bool? Forceshow { get; set; }
}