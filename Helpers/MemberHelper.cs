using Dapper;
using ForumDataMigration.Helper;
using Npgsql;

namespace ForumDataMigration.Helpers;

public class MemberHelper
{
    public static Dictionary<string, long> GetMemberNameDic()
    {
        const string sql = @"SELECT ""Id"",""Value"" FROM ""MemberExtendData"" med WHERE ""Key"" = 'OriginUsername'";

        var dic = CommonHelper.WatchTime(nameof(GetMemberNameDic)
                                       , () =>
                                         {
                                             using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

                                             var idDic = conn.Query<(long memberId, string userName)>(sql).ToDictionary(t => t.userName, t => t.memberId);

                                             return idDic;
                                         });

        return dic;
    }

    ///<summary>
    ///封鎖帳號
    ///</summary>
    public static HashSet<long> GetProhibitMemberIdHash()
    {
        const string sql = @"SELECT m.""Id"" FROM ""MemberGroup"" mg 
        INNER JOIN ""Member"" m ON m.""Id""  = mg.""Id"" 
        WHERE ""GroupId"" = 202359554410496";

        var hashSet = CommonHelper.WatchTime(nameof(GetProhibitMemberIdHash)
                                       , () =>
                                         {
                                             using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

                                             var idDic = conn.Query<long>(sql).ToHashSet();

                                             return idDic;
                                         });

        return hashSet;
    }
}