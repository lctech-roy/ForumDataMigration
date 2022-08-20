using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Netcorext.Algorithms;
using Netcorext.EntityFramework.UserIdentityPattern;

namespace ForumDataMigration;

public class AttachmentMigration
{
    private readonly ISnowflake _snowflake;
    private readonly DatabaseContext _context;

    public AttachmentMigration(ISnowflake snowflake, DatabaseContext context)
    {
        _snowflake = snowflake;
        _context = context;
    }

    public void Migration()
    {
        const string copyAttachmentSql = $"COPY \"{nameof(ExternalAttachmentUrl)}\" " +
                                         $"(\"{nameof(ExternalAttachmentUrl.AttachmentId)}\",\"{nameof(ExternalAttachmentUrl.Tid)}\",\"{nameof(ExternalAttachmentUrl.Pid)}\",\"{nameof(ExternalAttachmentUrl.AttachmentUrl)}\"" + Setting.COPY_SUFFIX;

        var periods = PeriodHelper.GetPeriods(2013,07);
        var postTableIds = ArticleHelper.GetPostTableIds();

        foreach (var period in periods)
        {
            var externalAttachmentUrlPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ExternalAttachmentUrl)}/{period.FolderName}";
            Directory.CreateDirectory(externalAttachmentUrlPath);

            Parallel.ForEach(postTableIds,
                             postTableId =>
                             {
                                 var queryAttachmentsSql = $@"SELECT tid,pid,message FROM pre_forum_post{(postTableId != 0 ? $"_{postTableId}" : "")} WHERE `first` = TRUE AND dateline >= @Start AND dateline < @End";

                                 try
                                 {
                                     var attachmentSb = new StringBuilder();
                                     using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                     var threadMessages = cn.Query<ThreadMessage>(queryAttachmentsSql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToArray();

                                     foreach (var threadMessage in threadMessages)
                                     {
                                         var externalImageUrls = threadMessage.Message.GetExternalImageUrls();

                                         foreach (var externalImageUrl in externalImageUrls)
                                         {
                                             attachmentSb.Append((string?) $"{_snowflake.Generate()}{Setting.D}{threadMessage.Tid}{Setting.D}{threadMessage.Pid}{Setting.D}{externalImageUrl}\n");
                                         }
                                     }

                                     if (attachmentSb.Length == 0) return;

                                     var insertAttachmentSql = string.Concat(copyAttachmentSql, attachmentSb);


                                     var fullPath = $"{externalAttachmentUrlPath}/{postTableId}.sql";
                                     File.WriteAllText(fullPath, insertAttachmentSql);
                                     Console.WriteLine(fullPath);
                                 }
                                 catch (Exception e)
                                 {
                                     Console.WriteLine(e);

                                     throw;
                                 }
                             });
        }
    }
}