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
}