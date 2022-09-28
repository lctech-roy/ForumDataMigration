using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Domain.Entities;
using Npgsql;

namespace ForumDataMigration;

public class Migration
{
    private const string SCHEMA_PATH = "../../../ScriptSchema";
    private const string BEFORE_FILE_NAME = "BeforeCopy.sql";
    private const string AFTER_FILE_NAME = "AfterCopy.sql";

    public void ExecuteRelation()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ArticleRelation)}/{BEFORE_FILE_NAME}");

        var periods = PeriodHelper.GetPeriods();

        foreach (var period in periods)
        {
            cn.ExecuteAllTextsIfExists($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRelation)}/{period.FileName}");
        }

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ArticleRelation)}/{AFTER_FILE_NAME}");
    }

    public async Task ExecuteArticleAsync(CancellationToken token)
    {
        const string articleSchemaPath = $"{SCHEMA_PATH}/{nameof(Article)}";
        const string articlePath = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}";
        const string articleCoverRelationSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleCoverRelation)}";
        const string articleCoverRelationPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}";

        await using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        await cn.ExecuteCommandByPathAsync($"{articleSchemaPath}/{BEFORE_FILE_NAME}", token);
        await cn.ExecuteCommandByPathAsync($"{articleCoverRelationSchemaPath}/{BEFORE_FILE_NAME}", token);

        var task = new Task(() => { FileHelper.ExecuteAllSqlFiles(articlePath, Setting.NEW_FORUM_CONNECTION); });

        var coverTask = new Task(() => { FileHelper.ExecuteAllSqlFiles(articleCoverRelationPath, Setting.NEW_FORUM_CONNECTION); });

        task.Start();
        coverTask.Start();

        await Task.WhenAll(task, coverTask);

        Console.WriteLine($"{nameof(ExecuteArticleAsync)} Done!");
    }

    public async Task ExecuteArticleRewardAsync(CancellationToken token)
    {
        const string articleRewardSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleReward)}";
        const string articleRewardPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}";

        await using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        await cn.ExecuteCommandByPathAsync($"{articleRewardSchemaPath}/{BEFORE_FILE_NAME}", token);

        cn.ExecuteAllTexts($"{articleRewardPath}.sql");
        
        await cn.ExecuteCommandByPathAsync($"{articleRewardSchemaPath}/{AFTER_FILE_NAME}", token);
        
        Console.WriteLine($"{nameof(ExecuteArticleRewardAsync)} Done!");
    }

    public void ExecuteAttachment()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);
        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ExternalAttachmentUrl)}/{BEFORE_FILE_NAME}");

        const string path = $"{Setting.INSERT_DATA_PATH}/{nameof(ExternalAttachmentUrl)}";

        var periods = PeriodHelper.GetPeriods();
        var postTableIds = ArticleHelper.GetPostTableIds();

        foreach (var filePath in periods.SelectMany(_ => postTableIds, (period, postTableId) => $"{path}/{period.FolderName}/{postTableId}.sql").Where(File.Exists))
        {
            cn.ExecuteAllTexts(filePath);
        }
    }

    public async Task ExecuteArticleVoteAsync(CancellationToken token)
    {
        const string articleVoteSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleVote)}";
        const string articleVotePath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVote)}";
        const string articleVoteItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItem)}";
        const string articleVoteItemHistoryPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItemHistory)}";

        await using var connection = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);
        await connection.ExecuteCommandByPathAsync($"{articleVoteSchemaPath}/{BEFORE_FILE_NAME}", token);

        var periods = PeriodHelper.GetPeriods();

        await using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        foreach (var filePath in periods.Select(period => $"{articleVotePath}/{period.FileName}").Where(File.Exists))
        {
            cn.ExecuteAllTexts(filePath);
        }

        foreach (var filePath in periods.Select(period => $"{articleVoteItemPath}/{period.FileName}").Where(File.Exists))
        {
            cn.ExecuteAllTexts(filePath);
        }

        foreach (var filePath in periods.Select(period => $"{articleVoteItemHistoryPath}/{period.FileName}").Where(File.Exists))
        {
            cn.ExecuteAllTexts(filePath);
        }

        await connection.ExecuteCommandByPathAsync($"{articleVoteSchemaPath}/{AFTER_FILE_NAME}", token);
    }

    public async Task ExecuteRatingAsync(CancellationToken token)
    {
        const string ratingSchemaPath = $"{SCHEMA_PATH}/Rating";
        const string ratingPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}";
        const string ratingItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}";

        await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
            await cn.ExecuteCommandByPathAsync($"{ratingSchemaPath}/{BEFORE_FILE_NAME}", token);

        var copyRatingTask = new Task(() =>
                                      {
                                          using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);
                                          cn.ExecuteAllCopyFiles(ratingPath);
                                      });

        var copyRatingItemTask = new Task(() =>
                                          {
                                              using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);
                                              cn.ExecuteAllCopyFiles(ratingItemPath);
                                          });

        copyRatingTask.Start();
        copyRatingItemTask.Start();
        await Task.WhenAll(copyRatingTask, copyRatingItemTask);

        await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
            await cn.ExecuteCommandByPathAsync($"{ratingSchemaPath}/{AFTER_FILE_NAME}", token);
    }

    public async Task ExecuteCommentAsync(CancellationToken token)
    {
        const string commentSchemaPath = $"{SCHEMA_PATH}/{nameof(Comment)}";
        const string commentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}";
        const string commentExtendDataPath = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}";

        await using (var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION))
            await cn.ExecuteCommandByPathAsync($"{commentSchemaPath}/{BEFORE_FILE_NAME}", token);
        
        var commentTask = new Task(() => { FileHelper.ExecuteAllSqlFiles(commentPath, Setting.NEW_COMMENT_CONNECTION); });
        var commentExtendDataTask = new Task(() => { FileHelper.ExecuteAllSqlFiles(commentExtendDataPath, Setting.NEW_COMMENT_CONNECTION); });
        
        commentTask.Start();
        commentExtendDataTask.Start();
        await Task.WhenAll(commentTask, commentExtendDataTask);
        
        await using (var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION))
            await cn.ExecuteCommandByPathAsync($"{commentSchemaPath}/{AFTER_FILE_NAME}", token);
    }


    public async Task ExecuteGameItemAsync()
    {
        await using var connection = new NpgsqlConnection(Setting.NEW_GAME_CENTER_CONNECTION);

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(BagGameItem)}/{BEFORE_FILE_NAME}");

        var bagTask = new Task(() => connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(Bag)}.sql"));

        var bagItemTask = new Task(() =>
                                   {
                                       using var connection2 = new NpgsqlConnection(Setting.NEW_GAME_CENTER_CONNECTION);
                                       connection2.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(BagGameItem)}.sql");
                                   });

        bagTask.Start();
        bagItemTask.Start();
        await Task.WhenAll(bagTask, bagItemTask);

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(BagGameItem)}/{AFTER_FILE_NAME}");
    }

    public async Task ExecuteMemberBagAsync()
    {
        await using var connection = new NpgsqlConnection(Setting.NEW_GAME_CENTER_MEDAL_CONNECTION);

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(MemberBagItem)}/{BEFORE_FILE_NAME}");

        var bagTask = new Task(() => connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(MemberBag)}.sql"));

        var bagItemTask = new Task(() =>
                                   {
                                       using var connection2 = new NpgsqlConnection(Setting.NEW_GAME_CENTER_MEDAL_CONNECTION);
                                       connection2.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(MemberBagItem)}.sql");
                                   });

        bagTask.Start();
        bagItemTask.Start();
        await Task.WhenAll(bagTask, bagItemTask);

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(MemberBagItem)}/{AFTER_FILE_NAME}");
    }
}