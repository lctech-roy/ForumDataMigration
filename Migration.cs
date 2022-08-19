using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Npgsql;

namespace ForumDataMigration;

public class Migration
{
    private const string SCHEMA_PATH = "../../../ScriptSchema";
    private const string BEFORE_FILE_NAME = "BeforeCopy.sql";
    private const string AFTER_FILE_NAME = "AfterCopy.sql";
    private const string CONNECTION_STR = Setting.NEW_FORUM_CONNECTION;

    public void ExecuteRelation()
    {
        using var cn = new NpgsqlConnection(CONNECTION_STR);

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ArticleRelation)}/{BEFORE_FILE_NAME}");

        var periods = PeriodHelper.GetPeriods();

        foreach (var period in periods)
        {
            cn.ExecuteAllTextsIfExists($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRelation)}/{period.FileName}");
        }
    }

    public async Task ExecuteArticleAsync(CancellationToken token)
    {
        const string articleSchemaPath = $"{SCHEMA_PATH}/{nameof(Article)}";
        const string articlePath = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}";
        const string articleRewardPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}";
        const string articleCoverRelationSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleCoverRelation)}";
        const string articleCoverRelationPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}";

        await using var cn = new NpgsqlConnection(CONNECTION_STR);

        await cn.ExecuteCommandByPathAsync($"{articleSchemaPath}/{BEFORE_FILE_NAME}", token);
        await cn.ExecuteCommandByPathAsync($"{articleCoverRelationSchemaPath}/{BEFORE_FILE_NAME}", token);

        var periods = PeriodHelper.GetPeriods();
        var postTableIds = ArticleHelper.GetPostTableIds();

        foreach (var period in periods)
        {
            // Parallel.ForEach(postTableIds,
            //                  postTableId =>
            //                  {
            //                      var filePath = $"{articlePath}/{period.FolderName}/{postTableId}.sql";
            //
            //                      if (!File.Exists(filePath)) return;
            //
            //                      using var connection = new NpgsqlConnection(CONNECTION_STR);
            //
            //                      connection.ExecuteAllTexts(filePath);
            //                  });
            //
            // Parallel.ForEach(postTableIds,
            //                  postTableId =>
            //                  {
            //                      var filePath = $"{articleCoverRelationPath}/{period.FolderName}/{postTableId}.sql";
            //
            //                      if (!File.Exists(filePath)) return;
            //
            //                      using var connection = new NpgsqlConnection(CONNECTION_STR);
            //
            //                      connection.ExecuteAllTexts(filePath);
            //                  });
            
            Parallel.ForEach(postTableIds,
                             postTableId =>
                             {
                                 var filePath = $"{articleRewardPath}/{period.FolderName}/{postTableId}.sql";

                                 if (!File.Exists(filePath)) return;

                                 using var connection = new NpgsqlConnection(CONNECTION_STR);

                                 connection.ExecuteAllTexts(filePath);
                             });
        }

        await cn.ExecuteCommandByPathAsync($"{articleSchemaPath}/{AFTER_FILE_NAME}", token);
    }

    public async Task ExecuteArticleRewardAsync(CancellationToken token)
    {
        const string articleRewardSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleReward)}";
        const string articleRewardPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}";
        
        await using var cn = new NpgsqlConnection(CONNECTION_STR);

        await cn.ExecuteCommandByPathAsync($"{articleRewardSchemaPath}/{BEFORE_FILE_NAME}", token);
        
        var periods = PeriodHelper.GetPeriods();
        var postTableIds = ArticleHelper.GetPostTableIds();
        
        foreach (var period in periods)
        {
            Parallel.ForEach(postTableIds,
                             postTableId =>
                             {
                                 var filePath = $"{articleRewardPath}/{period.FolderName}/{postTableId}.sql";

                                 if (!File.Exists(filePath)) return;

                                 using var connection = new NpgsqlConnection(CONNECTION_STR);

                                 connection.ExecuteAllTexts(filePath);
                             });
        }
        
        await cn.ExecuteCommandByPathAsync($"{articleRewardSchemaPath}/{AFTER_FILE_NAME}", token);
    }

    public void ExecuteAttachment()
    {
        using var cn = new NpgsqlConnection(CONNECTION_STR);
        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(ExternalAttachmentUrl)}/{BEFORE_FILE_NAME}");

        const string path = $"{Setting.INSERT_DATA_PATH}/{nameof(ExternalAttachmentUrl)}";

        var periods = PeriodHelper.GetPeriods();
        var postTableIds = ArticleHelper.GetPostTableIds();

        foreach (var period in periods)
        {
            foreach (var postTableId in postTableIds)
            {
                var filePath = $"{path}/{period.FolderName}/{postTableId}.sql";

                if (!File.Exists(filePath)) continue;

                cn.ExecuteAllTexts(filePath);
            }
        }
    }

    public async Task ExecuteArticleVoteAsync(CancellationToken token)
    {
        const string articleVoteSchemaPath = $"{SCHEMA_PATH}/{nameof(ArticleVote)}";
        const string articleVotePath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVote)}";
        const string articleVoteItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItem)}";
        const string articleVoteItemHistoryPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItemHistory)}";

        await using var connection = new NpgsqlConnection(CONNECTION_STR);
        await connection.ExecuteCommandByPathAsync($"{articleVoteSchemaPath}/{BEFORE_FILE_NAME}", token);

        var periods = PeriodHelper.GetPeriods();

        await using var cn = new NpgsqlConnection(CONNECTION_STR);

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

        await using var cn = new NpgsqlConnection(CONNECTION_STR);
        await cn.ExecuteCommandByPathAsync($"{ratingSchemaPath}/{BEFORE_FILE_NAME}", token);

        var periods = PeriodHelper.GetPeriods();

        foreach (var filePath in periods.Select(period => $"{ratingPath}/{period.FileName}").Where(filePath => File.Exists(filePath)))
        {
            cn.ExecuteAllTexts(filePath);
        }

        foreach (var filePath in periods.Select(period => $"{ratingItemPath}/{period.FileName}").Where(filePath => File.Exists(filePath)))
        {
            cn.ExecuteAllTexts(filePath);
        }

        await cn.ExecuteCommandByPathAsync($"{ratingSchemaPath}/{AFTER_FILE_NAME}", token);
    }


    public async Task ExecuteCommentAsync(CancellationToken token)
    {
        const string commentSchemaPath = $"{SCHEMA_PATH}/{nameof(Comment)}";
        const string commentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}";
        const string commentExtendDataPath = $"{Setting.INSERT_DATA_PATH}/{Setting.COMMENT_EXTEND_DATA}";

        await using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        await cn.ExecuteCommandByPathAsync($"{commentSchemaPath}/{BEFORE_FILE_NAME}", token);

        var periods = PeriodHelper.GetPeriods();
        var postTableIds = ArticleHelper.GetPostTableIds();

        var commentTask = new Task(() =>
                                   {
                                       foreach (var period in periods)
                                       {
                                           Parallel.ForEach(postTableIds,
                                                            postTableId =>
                                                            {
                                                                var filePath = $"{commentPath}/{period.FolderName}/{postTableId}.sql";

                                                                if (!File.Exists(filePath)) return;

                                                                using var connection = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

                                                                connection.ExecuteAllTexts(filePath);
                                                            });
                                       }
                                   });

        
        var commentExtendDataTask = new Task(() =>
                                             {
                                                 foreach (var period in periods)
                                                 {
                                                     Parallel.ForEach(postTableIds,
                                                                      postTableId =>
                                                                      {
                                                                          var filePath = $"{commentExtendDataPath}/{period.FolderName}/{postTableId}.sql";

                                                                          if (!File.Exists(filePath)) return;

                                                                          using var connection = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

                                                                          connection.ExecuteAllTexts(filePath);
                                                                      });
                                                 }
                                             });

        commentTask.Start();
        commentExtendDataTask.Start();
        await Task.WhenAll(commentTask, commentExtendDataTask);

        await cn.ExecuteCommandByPathAsync($"{commentSchemaPath}/{AFTER_FILE_NAME}", token);
    }
}