using Dapper;
using ForumDataMigration.Models;
using MySqlConnector;

namespace ForumDataMigration.Helper;

public static class CommentHelper
{
    public static IEnumerable<PreForumPostcomment> GetPostComments(int tid,uint pid)
    { 
        const string querySql = $"SELECT * FROM pre_forum_postcomment WHERE tid = @tid AND pid= @pid";

        using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        return cn.Query<PreForumPostcomment>(querySql, new { tid,pid }).ToList();
    }
}