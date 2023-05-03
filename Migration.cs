using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Comment.Domain.Entities;
using Lctech.Jkf.Forum.Domain.Entities;
using Lctech.Participle.Domain.Entities;
using Npgsql;

namespace ForumDataMigration;

public class Migration
{
    private const string SCHEMA_PATH = Setting.SCHEMA_PATH;
    private const string BEFORE_FILE_NAME = Setting.BEFORE_FILE_NAME;
    private const string AFTER_FILE_NAME = Setting.AFTER_FILE_NAME;

    public void ExecuteAttachment()
    {
        const string attachmentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}";

        var tableNumbers = AttachmentHelper.TableNumbers;

        CommonHelper.WatchTime(nameof(ExecuteAttachment),
                               () =>
                               {
                                   foreach (var tableNumber in tableNumbers)
                                   {
                                       FileHelper.ExecuteAllSqlFiles($"{attachmentPath}/{tableNumber}", Setting.NEW_ATTACHMENT_CONNECTION);
                                   }
                               });
    }

    public async Task ExecuteArticleAsync(CancellationToken token)
    {
        const string articlePath = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}";
        const string attachmentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}_{nameof(Article)}";
        const string articleAttachmentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleAttachment)}";

        // const string articleSchemaPath = $"{SCHEMA_PATH}/{nameof(Article)}";
        // const string attachmentSchemaPath = $"{SCHEMA_PATH}/{nameof(Attachment)}";

        // await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        // {
        //     await cn.ExecuteCommandByPathAsync($"{articleSchemaPath}/{BEFORE_FILE_NAME}", token);
        // }
        //
        // await using (var cn2 = new NpgsqlConnection(Setting.NEW_ATTACHMENT_CONNECTION))        
        //     await cn2.ExecuteCommandByPathAsync($"{attachmentSchemaPath}/{BEFORE_FILE_NAME}", token);

        var folderName = RetryHelper.GetArticleRetryDateStr();
        var periods = PeriodHelper.GetPeriods(folderName);

        if (Setting.USE_UPDATED_DATE)
        {
            var removeArticleTask = new Task(() => { RetryHelper.RemoveDataByDateStr(Setting.NEW_FORUM_CONNECTION, nameof(Article), folderName!); });

            var removeAttachmentTask = new Task(() => { RetryHelper.RemoveDataByDateStr(Setting.NEW_ATTACHMENT_CONNECTION, nameof(Attachment), folderName!); });

            removeArticleTask.Start();
            removeAttachmentTask.Start();

            await Task.WhenAll(removeArticleTask, removeAttachmentTask);
        }

        var task = new Task(() =>
                            {
                                foreach (var period in periods)
                                    FileHelper.ExecuteAllSqlFiles($"{articlePath}/{period.FolderName}", Setting.NEW_FORUM_CONNECTION);
                            });

        var articleAttachmentTask = new Task(() =>
                                             {
                                                 foreach (var period in periods)
                                                     FileHelper.ExecuteAllSqlFiles($"{articleAttachmentPath}/{period.FolderName}", Setting.NEW_FORUM_CONNECTION);
                                             });

        var attachmentTask = new Task(() =>
                                      {
                                          foreach (var period in periods)
                                              FileHelper.ExecuteAllSqlFiles($"{attachmentPath}/{period.FolderName}", Setting.NEW_ATTACHMENT_CONNECTION);
                                      });

        task.Start();
        articleAttachmentTask.Start();
        attachmentTask.Start();

        await Task.WhenAll(task, articleAttachmentTask, attachmentTask);

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

    public static async Task ExecuteArticleVoteAsync(CancellationToken token)
    {
        const string articleVoteSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleVote)}";
        const string articleVotePath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVote)}";
        const string articleVoteItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItem)}";
        const string articleVoteItemHistoryPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItemHistory)}";

        // await using var connection = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);
        // await connection.ExecuteCommandByPathAsync($"{articleVoteSchemaPath}/{BEFORE_FILE_NAME}", token);

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

        // await connection.ExecuteCommandByPathAsync($"{articleVoteSchemaPath}/{AFTER_FILE_NAME}", token);
    }

    public async Task ExecuteRatingAsync(CancellationToken token)
    {
        const string ratingSchemaPath = $"{SCHEMA_PATH}/Rating";
        const string ratingPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}";
        const string ratingItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}";

        // await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        //     await cn.ExecuteCommandByPathAsync($"{ratingSchemaPath}/{BEFORE_FILE_NAME}", token);

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

        // await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        //     await cn.ExecuteCommandByPathAsync($"{ratingSchemaPath}/{AFTER_FILE_NAME}", token);
    }

    public async Task ExecuteCommentAsync(CancellationToken token)
    {
        Thread.Sleep(3000);

        const string commentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}";
        const string commentExtendDataPath = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}";
        const string commentAttachmentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentAttachment)}";

        // const string attachmentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}_{nameof(Comment)}";

        // const string commentSchemaPath = $"{SCHEMA_PATH}/{nameof(Comment)}";
        // const string attachmentSchemaPath = $"{SCHEMA_PATH}/{nameof(Attachment)}";

        // await using (var cn2 = new NpgsqlConnection(Setting.NEW_ATTACHMENT_CONNECTION))        
        //     await cn2.ExecuteCommandByPathAsync($"{attachmentSchemaPath}/{BEFORE_FILE_NAME}", token);

        // await using (var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION))
        // {
        //     await cn.ExecuteCommandByPathAsync($"{commentAttachmentSchemaPath}/{BEFORE_FILE_NAME}", token);
        //
        //     await cn.ExecuteCommandByPathAsync($"{commentSchemaPath}/{BEFORE_FILE_NAME}", token);
        // }

        var folderName = RetryHelper.GetCommentRetryDateStr();
        var periods = PeriodHelper.GetPeriods(folderName);

        if (Setting.USE_UPDATED_DATE)
        {
            var removeComment = new Task(() => { RetryHelper.RemoveDataByDateStr(Setting.NEW_COMMENT_CONNECTION, nameof(Comment), folderName!); });

            var removeCommentExtendDataTask = new Task(() => { RetryHelper.RemoveDataByDateStr(Setting.NEW_COMMENT_CONNECTION, nameof(CommentExtendData), folderName!); });

            removeComment.Start();
            removeCommentExtendDataTask.Start();

            await Task.WhenAll(removeComment, removeCommentExtendDataTask);
        }

        var commentTask = new Task(() =>
                                   {
                                       foreach (var period in periods)
                                           FileHelper.ExecuteAllSqlFiles($"{commentPath}/{period.FolderName}", Setting.NEW_COMMENT_CONNECTION);
                                   });

        var commentExtendDataTask = new Task(() =>
                                             {
                                                 foreach (var period in periods)
                                                     FileHelper.ExecuteAllSqlFiles($"{commentExtendDataPath}/{period.FolderName}", Setting.NEW_COMMENT_CONNECTION);
                                             });

        var commentAttachmentTask = new Task(() =>
                                             {
                                                 foreach (var period in periods)
                                                     FileHelper.ExecuteAllSqlFiles($"{commentAttachmentPath}/{period.FolderName}", Setting.NEW_COMMENT_CONNECTION);
                                             });

        // var attachmentTask = new Task(() =>
        //                               {
        //                                   foreach (var period in periods)
        //                                       FileHelper.ExecuteAllSqlFiles($"{attachmentPath}/{period.FolderName}", Setting.NEW_ATTACHMENT_CONNECTION);
        //                               });

        commentTask.Start();
        commentExtendDataTask.Start();
        commentAttachmentTask.Start();

        // attachmentTask.Start();

        await Task.WhenAll(commentTask, commentExtendDataTask, commentAttachmentTask);

        // await using (var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION))
        //     await cn.ExecuteCommandByPathAsync($"{commentSchemaPath}/{AFTER_FILE_NAME}", token);
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

    public async Task ExecuteParticipleAsync()
    {
        await using var connection = new NpgsqlConnection(Setting.NEW_PARTICIPLE_CONNECTION);

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(SensitiveWordFilter)}/{BEFORE_FILE_NAME}");

        connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(SensitiveWordFilter)}.sql");

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(SensitiveWordFilter)}/{AFTER_FILE_NAME}");
    }

    public async Task ExecuteArticleBlackListMemberAsync()
    {
        await using var connection = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ArticleBlackListMember)}/{BEFORE_FILE_NAME}");

        connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleBlackListMember)}.sql");

        connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ArticleBlackListMember)}/{AFTER_FILE_NAME}");
    }
    
    public async Task ExecuteTaskAsync()
    {
        await using var connection = new NpgsqlConnection(Setting.NEW_TASK_CONNECTION);

        // connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(Lctech.TaskCenter.Domain.Entities.Task)}/{BEFORE_FILE_NAME}");

        connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(Lctech.TaskCenter.Domain.Entities.Task)}.sql");
        connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(Lctech.TaskCenter.Domain.Entities.TaskExtendData)}.sql");
        connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(Lctech.TaskCenter.Domain.Entities.TaskRelation)}.sql");
        connection.ExecuteAllTexts($"{Setting.INSERT_DATA_PATH}/{nameof(Lctech.TaskCenter.Domain.Entities.TaskReward)}.sql");
        
        // connection.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(Lctech.TaskCenter.Domain.Entities.Task)}/{AFTER_FILE_NAME}");
    }
}