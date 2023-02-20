using Dapper;
using ForumDataMigration.Models;
using MySqlConnector;

namespace ForumDataMigration.Helper;

public static class ArticleHelper
{
    private static List<int> PostTableIds { get; }

    static ArticleHelper()
    {
        const string getTableIdsSql = @"
        SELECT DISTINCT(a.tableId) FROM (
            SELECT CONVERT(SUBSTRING_INDEX(TABLE_NAME,'_', -1),UNSIGNED) AS tableId FROM information_schema.tables
            WHERE table_schema = 'newjk' AND TABLE_NAME LIKE 'pre_forum_post_%'
        ) a
        WHERE a.tableId >= 0
        ORDER BY a.tableId";
        
        using var sqlConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
        PostTableIds = sqlConnection.Query<int>(getTableIdsSql).ToList();
    }

    public static List<int> GetPostTableIds(int? id = null)
    {
        if (Setting.TestTid != null)
        {
            const string getPidSql = $@"SELECT posttableid FROM pre_forum_thread where tid =@tid";
            using var sqlConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
            id = sqlConnection.QueryFirst<int>(getPidSql, new { tid = Setting.TestTid });
        }

        return id.HasValue ? new List<int> { id.Value } : PostTableIds;
    }

    public static Dictionary<(int, string), int?> GetModDic()
    {
        const string queryModSql = $"select tid, action, max(expiration) as expiration from pre_forum_threadmod WHERE expiration != 0 AND action in ('EST','EDI','EHL','BNP','UBN','ECL') GROUP BY tid,`action`";

        var modDic = new Dictionary<(int, string), int?>();

        CommonHelper.WatchTime(nameof(GetModDic),
                               () =>
                               {
                                   using (var conn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                   {
                                       conn.Open();

                                       using (var command = new MySqlCommand(queryModSql, conn))
                                       {
                                           var reader = command.ExecuteReader();

                                           while (reader.Read())
                                           {
                                               modDic.Add((reader.GetInt32(0), reader.GetString(1)), reader.GetInt32(2));
                                           }

                                           reader.Close();
                                       }
                                   }
                               });

        return modDic;
    }

    public static Dictionary<long, Read> GetReadDic()
    {
        Dictionary<long, Read> readDic = default!;

        CommonHelper.WatchTime(nameof(GetReadDic),
                               () =>
                               {
                                   using var readConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                   const string readSql = @"SELECT tid, MAX(uid) AS ReadUid, MAX(pid) AS ReadFloor FROM pre_forum_read WHERE pid <> -1 GROUP BY tid";

                                   readDic = readConnection.Query<Read>(readSql).ToDictionary(row => row.Tid, row => row);
                               });

        return readDic;
    }

    public static CommonSetting GetCommonSetting()
    {
        const string querySetting = @"SELECT * FROM pre_common_setting WHERE skey IN ('rewardexpiration','hideexpiration')";

        var commonSetting = new CommonSetting();
        
        CommonHelper.WatchTime(nameof(GetCommonSetting),
                               () =>
                               {
                                   using var conn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
                                   conn.Open();

                                   using var command = new MySqlCommand(querySetting, conn);

                                   var reader = command.ExecuteReader();

                                   while (reader.Read())
                                   {
                                       var key = reader.GetString(0);
                                       var value = reader.GetString(1);

                                       switch (key)
                                       {
                                           case "rewardexpiration":
                                               commonSetting.RewardExpirationDay = Convert.ToInt32(value);

                                               break;
                                           case "hideexpiration":
                                               commonSetting.HideExpirationDay = Convert.ToInt32(value);

                                               break;
                                       }
                                   }

                                   reader.Close();
                               });
        return commonSetting;
    }
}