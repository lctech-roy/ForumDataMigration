using System.Data;
using Dapper;
using ForumDataMigration.Models;
using Npgsql;

namespace ForumDataMigration.Helper;

public static class RelationHelper
{
    public static Dictionary<int, long> GetArticleDic()
    {
        const string queryRelationSql = $"select \"{nameof(ArticleRelation.Id)}\",\"{nameof(ArticleRelation.Tid)}\" from \"{nameof(ArticleRelation)}\"";

        var relationDic = new Dictionary<int, long>();

        using (var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        {
            conn.Open();

            using (var command = new NpgsqlCommand(queryRelationSql, conn))
            {
                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    relationDic.Add(reader.GetInt32(1), reader.GetInt64(0));
                }

                reader.Close();
            }
        }

        Console.Out.WriteLine("Finish Import ArticleRelationDic!");

        return relationDic;
    }

    public static Dictionary<int, long> GetBoardDic()
    {
        const string queryBoardSql = $"SELECT \"Id\",\"Fid\" from \"BoardRelation\"";

        var boardDic = new Dictionary<int, long>();

        using (var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        {
            conn.Open();

            using (var command = new NpgsqlCommand(queryBoardSql, conn))
            {
                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    boardDic.Add(reader.GetInt32(1), reader.GetInt64(0));
                }

                reader.Close();
            }
        }

        Console.WriteLine("Finish Import BoardDic!");

        return boardDic;
    }

    public static Dictionary<int, long?> GetCategoryDic()
    {
        const string queryCategorySql = $"SELECT \"Id\",\"TypeId\" FROM \"ArticleCategoryRelation\"";

        var categoryDic = new Dictionary<int, long?>();

        using (var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        {
            conn.Open();

            using (var command = new NpgsqlCommand(queryCategorySql, conn))
            {
                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    categoryDic.Add(reader.GetInt32(1), reader.GetInt64(0));
                }

                reader.Close();
            }
        }

        Console.WriteLine("Finish Import CategoryDic!");

        return categoryDic;
    }

    public static Dictionary<long, long> GetMemberUidDic()
    {
        const string queryMemberSql = $"SELECT \"Id\",\"Uid\" FROM \"Member\"";

        using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

        var idDic = conn.Query<(long id, long uid)>(queryMemberSql).ToDictionary(t => t.uid, t => t.id);
        
        Console.WriteLine("Finish Import MemberUidDic!");

        return idDic;
    }

    public static Dictionary<long, long> GetGameItemRelationDic()
    {
        const string queryGameItemRelationSql = @"SELECT ""Id"",""MaterialId"" FROM ""GameItemRelation""";
        
        using var conn = new NpgsqlConnection(Setting.NEW_GAME_CENTER_CONNECTION);

        var idDic = conn.Query<(long id, long materialId)>(queryGameItemRelationSql).ToDictionary(t => t.materialId, t => t.id);

        return idDic;
    }
    
    public static Dictionary<long, long> GetMedalRelationDic()
    {
        const string queryMedalRelationSql = @"SELECT ""Id"",""OriMedalId"" FROM ""MedalRelation""";
        
        using var conn = new NpgsqlConnection(Setting.NEW_GAME_CENTER_MEDAL_CONNECTION);

        var idDic = conn.Query<(long id, long medalId)>(queryMedalRelationSql).ToDictionary(t => t.medalId, t => t.id);

        return idDic;
    }

    public static async Task<Dictionary<int, long>> GetArticleIdDicAsync(int[] tids, CancellationToken cancellationToken = default)
    {
        const string queryArticleIdSql = @"SELECT ""Id"",""Tid"" FROM ""ArticleRelation"" WHERE ""Tid"" =ANY(@tids)";

        await using var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        var idDic = (await conn.QueryAsync<(long id, int tid)>(new CommandDefinition(queryArticleIdSql, new { tids }, cancellationToken: cancellationToken)))
           .ToDictionary(t => t.tid, t => t.id);

        return idDic;
    }

    public static Dictionary<long, (long, string)> GetSimpleMemberDic()
    {
        const string queryMemberSql = $"SELECT \"Id\",\"Uid\",\"DisplayName\" FROM \"Member\"";
        
        using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

        var simpleMemberDic = conn.Query<(long id, long uid, string displayName)>(queryMemberSql).ToDictionary(t => t.uid, t => (t.id, t.displayName));

        return simpleMemberDic;
    }
    
    public static async Task<Dictionary<long, (long, string)>> GetSimpleMemberDicAsync(int[] uids, CancellationToken cancellationToken = default)
    {
        const string queryMemberSql = $"SELECT \"Id\",\"Uid\",\"DisplayName\" FROM \"Member\" WHERE \"Uid\" = ANY(@uids)";

        await using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

        var simpleMemberDic = (await conn.QueryAsync<(long id, long uid, string displayName)>(new CommandDefinition(queryMemberSql, new { uids }, cancellationToken: cancellationToken)))
           .ToDictionary(t => t.uid, t => (t.id, t.displayName));

        return simpleMemberDic;
    }

    public static async Task<Dictionary<string, long>> GetMembersDisplayNameDicAsync(string[] displayNames, CancellationToken cancellationToken = default)
    {
        const string queryMemberSql = $"SELECT \"Id\",\"DisplayName\" FROM \"Member\" WHERE \"DisplayName\" = ANY(@displayNames)";

        await using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

        var membersDisplayNmaeDic = (await conn.QueryAsync<(long id, string displayName)>(new CommandDefinition(queryMemberSql, new { displayNames }, cancellationToken: cancellationToken)))
           .ToDictionary(t => t.displayName, t => t.id);

        return membersDisplayNmaeDic;
    }

    public static async Task<Dictionary<int, long>> GetMembersUidDicAsync(int[] uids, CancellationToken cancellationToken = default)
    {
        const string queryMemberSql = $"SELECT \"Id\",\"Uid\" FROM \"Member\" WHERE \"Uid\" = ANY(@uids)";

        await using var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION);

        var membersUidDic = (await conn.QueryAsync<(long id, int uid)>(new CommandDefinition(queryMemberSql, new { uids }, cancellationToken: cancellationToken)))
           .ToDictionary(t => t.uid, t => t.id);

        return membersUidDic;
    }
}