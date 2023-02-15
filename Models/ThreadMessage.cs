namespace ForumDataMigration.Models;

public class ThreadMessage
{
    public int Tid { get; set; }
    public int Pid { get; set; }
    public string Message { get; set; } = default!;
}