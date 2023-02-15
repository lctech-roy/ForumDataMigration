using Dapper;
using ForumDataMigration.Models;
using MySqlConnector;

namespace ForumDataMigration.Helper;

public static class ArticleHelper
{
    private static List<int> PostTableIds { get; }

    static ArticleHelper()
    {
        var postTableIds = new List<int>();

        for (var i = 0; i <= 150; i++)
        {
            postTableIds.Add(i);
        }

        PostTableIds = postTableIds;
    }

    public static List<int> GetPostTableIds(int? id = null)
    {
        return id.HasValue ? new List<int> { id.Value } : PostTableIds;
    }

    public static Dictionary<(int, string), int?> GetModDic()
    {
        const string queryModSql = $"select tid, action, max(expiration) as expiration from pre_forum_threadmod WHERE expiration != 0 AND action in ('EST','EDI','EHL','BNP','UBN','ECL') GROUP BY tid,`action`";

        var modDic = new Dictionary<(int, string), int?>();

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

        Console.WriteLine("Finish Import ModDic!");

        return modDic;
    }

    public static Dictionary<long, Read> GetReadDic()
    {
        using var readConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        const string readSql = @"SELECT tid, MAX(uid) AS ReadUid, MAX(pid) AS ReadFloor FROM pre_forum_read WHERE pid <> -1 GROUP BY tid";

        return readConnection.Query<Read>(readSql).ToDictionary(row => row.Tid, row => row);
    }

    public static CommonSetting GetCommonSetting()
    {
        const string querySetting = @"SELECT * FROM pre_common_setting WHERE skey IN ('rewardexpiration','hideexpiration')";

        using var conn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
        conn.Open();

        using var command = new MySqlCommand(querySetting, conn);

        var reader = command.ExecuteReader();

        var commonSetting = new CommonSetting();

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

        return commonSetting;
    }
}