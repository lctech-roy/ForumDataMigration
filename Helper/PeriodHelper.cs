namespace ForumDataMigration.Helper;

public static class PeriodHelper
{
    public static List<Period>  Periods { get; }

    static PeriodHelper()
    {
        var startDate = new DateTimeOffset(2007, 12, 1, 0, 0, 0, TimeSpan.FromHours(8));
        
        var startSeconds = startDate.ToUnixTimeSeconds();
        var endDate = startDate.AddMonths(1);
        var endSeconds = endDate.ToUnixTimeSeconds();
        var finalSeconds = DateTimeOffset.UtcNow.AddMonths(1).ToUnixTimeSeconds();

        void ToNextPeriod()
        {
            startDate = startDate.AddMonths(1);
            endDate = endDate.AddMonths(1);
            startSeconds = startDate.ToUnixTimeSeconds();
            endSeconds = endDate.ToUnixTimeSeconds();
        }

        var periods = new List<Period>();

        while (endSeconds < finalSeconds)
        {
            var dateStr = $"{startDate.Year}{startDate.Month.ToString().PadLeft(2, '0')}";
            periods.Add(new Period
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            StartSeconds = startSeconds,
                            EndSeconds = endSeconds,
                            FileName = $"{dateStr}.sql",
                            FolderName =  dateStr
                        });

            ToNextPeriod();
        }

        Periods = periods;
    }

    public static List<Period> GetPeriods(int? year =null ,int? month = null)
    {
        if (!year.HasValue && !month.HasValue)
            return Periods;
        
        var startDate = new DateTimeOffset(year!.Value, month!.Value, 1, 0, 0, 0, TimeSpan.FromHours(8));
        var startSeconds = startDate.ToUnixTimeSeconds();

        return Periods.Where(x => x.StartSeconds >= startSeconds).ToList();
    }
}