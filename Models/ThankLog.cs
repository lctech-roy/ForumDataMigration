namespace ForumDataMigration.Models;

public class ThankLog
{
    public uint Id { get; set; }
    public string? Fromuser { get; set; }
    public int Fromuseruid { get; set; }
    public string? Touser { get; set; }
    public string Tid { get; set; } = null!;
    public string Aswhat { get; set; } = null!;
    public int Logdate { get; set; }
    public int Touseruid { get; set; }
}