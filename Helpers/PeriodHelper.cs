using Dapper;
using MySqlConnector;

namespace ForumDataMigration.Helper;

public static class PeriodHelper
{
    private static List<Period> Periods { get; }

    static PeriodHelper()
    {
        var startDate = new DateTimeOffset(2007, 12, 1, 0, 0, 0, TimeSpan.FromHours(0));

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
            var dateStr = ConvertToDateStr(startDate);

            periods.Add(new Period
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            StartSeconds = startSeconds,
                            EndSeconds = endSeconds,
                            FileName = $"{dateStr}.sql",
                            FolderName = dateStr
                        });

            ToNextPeriod();
        }

        Periods = periods;
    }

    public static string ConvertToDateStr(DateTimeOffset dateTimeOffset)
    {
        return $"{dateTimeOffset.Year}{dateTimeOffset.Month.ToString().PadLeft(2, '0')}";
    }

    public static List<Period> GetPeriods(string? dateStr = null)
    {
        if (Setting.TestTid != null)
        {
            const string getDatelineSql = $@"SELECT dateline FROM pre_forum_thread where tid =@tid";
            using var sqlConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
            var timeSeconds = sqlConnection.QueryFirst<int>(getDatelineSql, new { tid = Setting.TestTid });
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(timeSeconds);
            
            return GetPeriods(dateTime.Year, dateTime.Month);
        }
        
        if (dateStr == null)
            return GetPeriods(null, null);

        var year = int.Parse(dateStr[..4]);
        var month = int.Parse(dateStr.Substring(4, 2));

        return GetPeriods(year, month);
    }

    public static List<Period> GetPeriods(int? year, int? month)
    {
        if (!year.HasValue && !month.HasValue)
            return Periods;

        var startDate = new DateTimeOffset(year!.Value, month!.Value, 1, 0, 0, 0, TimeSpan.FromHours(0));
        var startSeconds = startDate.ToUnixTimeSeconds();

        return Periods.Where(x => x.StartSeconds >= startSeconds).ToList();
    }
}