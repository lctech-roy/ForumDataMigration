using Dapper;
using ForumDataMigration.Helper;
using Npgsql;

namespace ForumDataMigration.Helpers;

public static class RelationHelper
{
    public static HashSet<long> GetArticleIdHash()
    {
        const string queryArticleIdSql = @"SELECT ""Id"" FROM ""Board""";
        
        var articleIdHash = new HashSet<long>();
        
        CommonHelper.WatchTime(nameof(GetBoardIdHash),
                               () =>
                               {
                                   using var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

                                   conn.Open();

                                   using var command = new NpgsqlCommand(queryArticleIdSql, conn);

                                   var reader = command.ExecuteReader();

                                   while (reader.Read())
                                   {
                                       articleIdHash.Add(reader.GetInt64(0));
                                   }

                                   reader.Close();
                               });

        return articleIdHash;
    }

    public static HashSet<long> GetBoardIdHash()
    {
        const string queryBoardIdSql = @"SELECT ""Id"" FROM ""Board""";
        
        var boardIdHash = new HashSet<long>();

        CommonHelper.WatchTime(nameof(GetBoardIdHash),
                               () =>
                               {
                                   using var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

                                   conn.Open();

                                   using var command = new NpgsqlCommand(queryBoardIdSql, conn);

                                   var reader = command.ExecuteReader();

                                   while (reader.Read())
                                   {
                                       boardIdHash.Add(reader.GetInt64(0));
                                   }

                                   reader.Close();
                               });
        
        return boardIdHash;
    }

    public static  HashSet<long> GetCategoryIdHash()
    {
        const string queryCategoryIdSql = $"SELECT \"Id\" FROM \"ArticleCategory\"";

        var categoryIdHash = new HashSet<long>();

        CommonHelper.WatchTime(nameof(GetCategoryIdHash),
                               () =>
                               {
                                   using var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

                                   conn.Open();

                                   using var command = new NpgsqlCommand(queryCategoryIdSql, conn);

                                   var reader = command.ExecuteReader();

                                   while (reader.Read())
                                   {
                                       categoryIdHash.Add(reader.GetInt64(0));
                                   }

                                   reader.Close();
                               });

        return categoryIdHash;
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
}