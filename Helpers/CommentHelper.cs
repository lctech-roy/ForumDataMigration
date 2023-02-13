using Dapper;
using ForumDataMigration.Models;
using MySqlConnector;

namespace ForumDataMigration.Helper;

public static class CommentHelper
{
    public static async Task<IEnumerable<PreForumPostcomment>> GetPostCommentsAsync(int tid, int pid, CancellationToken cancellationToken)
    {
        const string querySql = $"SELECT * FROM pre_forum_postcomment WHERE tid = @tid AND pid= @pid";

        await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        return (await cn.QueryAsync<PreForumPostcomment>(new CommandDefinition(querySql, new { tid, pid }, cancellationToken: cancellationToken))).ToList();
    }
}