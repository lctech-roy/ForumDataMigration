using Netcorext.Algorithms;
using Netcorext.EntityFramework.UserIdentityPattern;


namespace ForumDataMigration;

public class StatisticsMigration
{
    private readonly ISnowflake _snowflake;
    private readonly DatabaseContext _context;

    public StatisticsMigration(ISnowflake snowflake, DatabaseContext context)
    {
        _snowflake = snowflake;
        _context = context;
    }
    
    public void Migration()
    {
        // var cn = _oldForumContext.Database.GetDbConnection();
        //
        // const string sql = @"select logdate, fid, value from pre_forum_statlog LIMIT @Limit OFFSET @Offset";
        //
        // var statistics = cn.Query<Statistics>(sql, new { Limit = 100, Offset = 0 }).ToList();
        
        Console.Write("111");
    }
}