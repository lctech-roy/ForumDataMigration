using System.Data;
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

    public static Dictionary<int, long> GetCategoryDic()
    {
        const string queryCategorySql = $"SELECT \"Id\",\"TypeId\" FROM \"ArticleCategoryRelation\"";

        var categoryDic = new Dictionary<int, long>();

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

    public static Dictionary<int, long> GetMemberUidDic()
    {
        const string queryMemberSql = $"SELECT \"Id\",\"Value\" AS Uid FROM \"MemberProfile\" WHERE \"Key\"='PanUid'";

        var memberDic = new Dictionary<int, long>();

        using (var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION))
        {
            conn.Open();

            using (var command = new NpgsqlCommand(queryMemberSql, conn))
            {
                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    memberDic.Add(Convert.ToInt32(reader.GetString(1)), reader.GetInt64(0));
                }

                reader.Close();
            }
        }

        Console.WriteLine("Finish Import MemberUidDic!");

        return memberDic;
    }

    public static Dictionary<string, long> GetMemberDisplayNameDic()
    {
        const string queryMemberSql = $"SELECT \"Id\",\"Value\" AS DisplayName FROM \"MemberProfile\" WHERE \"Key\"='DisplayName'";

        var memberDic = new Dictionary<string, long>();

        using (var conn = new NpgsqlConnection(Setting.NEW_MEMBER_CONNECTION))
        {
            conn.Open();

            using (var command = new NpgsqlCommand(queryMemberSql, conn))
            {
                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    memberDic.Add(reader.GetString(1), reader.GetInt64(0));
                }

                reader.Close();
            }
        }

        Console.WriteLine("Finish Import MemberDisplayNameDic!");

        return memberDic;
    }
}