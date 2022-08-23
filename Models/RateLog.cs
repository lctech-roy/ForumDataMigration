namespace ForumDataMigration.Models;

public class RateLog
{
    public int Tid { get; set; }
    public int Uid { get; set; }
    public byte Extcredits { get; set; }
    public int Fid { get; set; }
    public ushort Typeid { get; set; }
    public uint Dateline { get; set; }
    public short Score { get; set; }
    public string Reason { get; set; } = null!;
}