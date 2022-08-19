namespace ForumDataMigration;

public class Period
{
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public long StartSeconds { get; set; }
    public long EndSeconds { get; set; }

    public string FileName { get; set; } = default!;
    public string FolderName { get; set; } = default!;
}