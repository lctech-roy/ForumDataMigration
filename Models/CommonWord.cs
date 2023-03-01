namespace ForumDataMigration.Models;

public class CommonWord
{
    public int Id { get; set; }
    public string Find { get; set; } = default!;
    public string Replacement { get; set; } = default!;
}