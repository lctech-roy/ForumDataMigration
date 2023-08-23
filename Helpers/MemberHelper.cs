using Dapper;
using ForumDataMigration.Helper;
using MySqlConnector;
using Npgsql;

namespace ForumDataMigration.Helpers;

public class MemberHelper
{
    public static Dictionary<string, long> GetMemberNameDic()
    {
        const string sql = @"SELECT uid,username FROM pre_ucenter_members 
                             UNION ALL
                             SELECT uid,username FROM pre_common_member WHERE uid NOT IN (SELECT uid FROM pre_ucenter_members)";

        var dic = CommonHelper.WatchTime(nameof(GetMemberNameDic)
                                       , () =>
                                         {
                                             using var conn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                             var idDic = conn.Query<(long memberId, string userName)>(sql).ToDictionary(t => t.userName, t => t.memberId);

                                             return idDic;
                                         });

        return dic;
    }

    ///<summary>
    ///封鎖帳號,禁止發言
    ///</summary>
    public static HashSet<long> GetProhibitMemberIdHash()
    {
        const string sql = @"SELECT @dateNow := UNIX_TIMESTAMP(NOW());
                             SELECT uid FROM pre_common_member WHERE 
                             (groupid = 4 AND (groupexpiry = 0 OR groupexpiry > @dateNow)) OR 
                             (groupid = 5 AND (groupexpiry = 0 OR groupexpiry > @dateNow)) OR
                             (extgroupids = '4') OR (extgroupids = '5')";

        var hashSet = CommonHelper.WatchTime(nameof(GetProhibitMemberIdHash)
                                       , () =>
                                         {
                                             using var conn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                             var idDic = conn.Query<long>(sql).ToHashSet();

                                             return idDic;
                                         });

        return hashSet;
    }
}