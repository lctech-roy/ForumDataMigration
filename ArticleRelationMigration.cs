using Netcorext.Algorithms;
using Dapper;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using MySqlConnector;

namespace ForumDataMigration;

public class ArticleRelationMigration
{
    private readonly ISnowflake _snowflake;

    public ArticleRelationMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        const string path = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRelation)}";
        Directory.CreateDirectory(path);

        const string sql = @"SELECT tid FROM pre_forum_thread WHERE dateline >= @Start AND dateline < @End";

        var periods = PeriodHelper.GetPeriods();
        
        Parallel.ForEach(periods,
                         period =>
                         {
                             var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
                             var tids = cn.Query<uint>(sql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToArray();
                             
                             if(!tids.Any()) return;
                             
                             var values = string.Join(string.Empty, tids.Select(tid => $"{_snowflake.Generate()}{Setting.D}{tid}\n"));
                             var insertSql = $"COPY \"{nameof(ArticleRelation)}\" (\"{nameof(ArticleRelation.Id)}\",\"{nameof(ArticleRelation.Tid)}\")" +
                                                    $" FROM STDIN (DELIMITER '{Setting.D}')\n{values}";
                             
                             File.WriteAllText($"{path}/{period.FileName}", insertSql);
                             Console.WriteLine(period.FileName);
                         });
    }
}