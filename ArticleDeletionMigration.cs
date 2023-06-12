using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using MySqlConnector;

namespace ForumDataMigration;

public class ArticleDeletionMigration
{
    private const string QUERY_TREAD_MOD_SQL = @"SELECT DISTINCT b.tid AS Id,b.uid AS DeleterId,b.dateline AS DeletionDateInt,b.reason AS DeletionReason FROM
                                                (SELECT tid,MAX(dateline) as dateline FROM pre_forum_threadmod
                                                WHERE action='DEL'
                                                GROUP BY tid) a
                                                INNER JOIN pre_forum_threadmod b ON a.tid = b.tid AND a.dateline = b.dateline";
    
    private const string COPY_ARTICLE_DELETION_PREFIX = $"COPY \"{nameof(ArticleDeletion)}\" " +
                                              $"(\"{nameof(ArticleDeletion.Id)}\",\"{nameof(ArticleDeletion.DeleterId)}\",\"{nameof(ArticleDeletion.DeletionDate)}\",\"{nameof(ArticleDeletion.DeletionReason)}\"" + Setting.COPY_SUFFIX;
    
    private const string ARTICLE_DELETION_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleDeletion)}.sql";
    
    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        ArticleDeletion[]? articleDeletions = null!;

        await CommonHelper.WatchTimeAsync(nameof(QUERY_TREAD_MOD_SQL),
                                          async () =>
                                          {
                                              await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                              var command = new CommandDefinition(QUERY_TREAD_MOD_SQL, cancellationToken: cancellationToken);

                                              articleDeletions = (await cn.QueryAsync<ArticleDeletion>(command)).ToArray();
                                          });

        foreach (var threadMod in articleDeletions)
        {
            threadMod.DeletionDate = DateTimeOffset.FromUnixTimeSeconds(threadMod.DeletionDateInt);
        }

        var articleDeletionSb = new StringBuilder();

        foreach (var articleDeletion in articleDeletions)
        {
            articleDeletionSb.AppendValueLine(articleDeletion.Id, articleDeletion.DeleterId, articleDeletion.DeletionDate, articleDeletion.DeletionReason.ToCopyText());
        }
        
        await File.WriteAllTextAsync($"{ARTICLE_DELETION_PATH}", string.Concat(COPY_ARTICLE_DELETION_PREFIX, articleDeletionSb.ToString()), cancellationToken);
    }
}