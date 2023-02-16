using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Netcorext.Algorithms;
using Npgsql;
using Polly;

namespace ForumDataMigration;

public class AttachmentMigration
{
    private const string ATTACHMENT_PREFIX = $"COPY \"{nameof(Attachment)}\" " +
                                             $"(\"{nameof(Attachment.Id)}\",\"{nameof(Attachment.Size)}\",\"{nameof(Attachment.ExternalLink)}\",\"{nameof(Attachment.Bucket)}\"" +
                                             $",\"{nameof(Attachment.DownloadCount)}\",\"{nameof(Attachment.ProcessingState)}\",\"{nameof(Attachment.DeleteStatus)}\",\"{nameof(Attachment.IsPublic)}\"" +
                                             $",\"{nameof(Attachment.StoragePath)}\",\"{nameof(Attachment.Name)}\",\"{nameof(Attachment.ContentType)}\",\"{nameof(Attachment.ParentId)}\"" +
                                             Setting.COPY_ENTITY_SUFFIX;

    private const string ATTACHMENT_RELATION_PREFIX = $"COPY \"{nameof(AttachmentRelation)}\" " +
                                                      $"(\"{nameof(AttachmentRelation.Pid)}\",\"{nameof(AttachmentRelation.Aid)}\",\"{nameof(AttachmentRelation.Id)}\"" +
                                                      Setting.COPY_SUFFIX;

    private const string ATTACHMENT_RELATION_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(AttachmentRelation)}";
    
    private static readonly ISnowflake AttachmentSnowflake = new SnowflakeJavaScriptSafeInteger(3);
    
    public static async Task MigrationAsync(CancellationToken cancellationToken)
    {
        int? progressedMaxAid;

        await using (var conn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        {
            //先建表
            await conn.ExecuteCommandByPathAsync($"{Setting.SCHEMA_PATH}/{nameof(AttachmentRelation)}/{Setting.BEFORE_FILE_NAME}", cancellationToken);
            const string getMaxAId = $@"SELECT MAX(""Aid"") FROM ""AttachmentRelation""";
            progressedMaxAid = conn.QueryFirst<int?>(getMaxAId);
            
            Console.WriteLine("progressedMaxAid:" + progressedMaxAid);
        }

        if(!progressedMaxAid.HasValue)
            FileHelper.RemoveFiles(new[] { Setting.ATTACHMENT_PATH, ATTACHMENT_RELATION_PATH });
        
        var tableNumbers = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        await Parallel.ForEachAsync(tableNumbers, CommonHelper.GetParallelOptions(cancellationToken), async (tableNumber, token) =>
                                                                                                      {
                                                                                                          var startAid = progressedMaxAid ?? 0;

                                                                                                          var sql = $@"SELECT a.tid,a.pid,a.aid,attachment AS ExternalLink,remote,isimage,
                                                                                                                       filename AS 'NAME',filesize AS Size,dateline,aa.downloads AS DownloadCount
                                                                                                                       FROM pre_forum_attachment_{tableNumber} a
                                                                                                                       LEFT JOIN pre_forum_attachment aa ON a.aid = aa.aid
                                                                                                                       WHERE a.aid >= @startAid
                                                                                                                       LIMIT 50000";

                                                                                                          await using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                                                          while (true)
                                                                                                          {
                                                                                                              var command = new CommandDefinition(sql, new { startAid }, cancellationToken: token);
                                                                                                              var attachments = (await cn.QueryAsync<Attachment>(command)).ToArray();

                                                                                                              if (!attachments.Any())
                                                                                                                  return;

                                                                                                              await ExecuteAsync(attachments, tableNumber, attachments.First().Aid);

                                                                                                              startAid = attachments[^1].Aid + 1;
                                                                                                          }
                                                                                                      });
    }

    private static async Task ExecuteAsync(IEnumerable<Attachment> attachments, int tableNumber, int startAid)
    {
        var attachmentSb = new StringBuilder();
        var attachmentRelationSb = new StringBuilder();

        foreach (var attachment in attachments)
        {
            var newId = Policy

                        // 1. 處理甚麼樣的例外
                       .Handle<ArgumentOutOfRangeException>()

                        // 2. 重試策略，包含重試次數
                       .Retry(5, (ex, retryCount) =>
                                 {
                                     Console.WriteLine($"發生錯誤：{ex.Message}，第 {retryCount} 次重試");
                                     Thread.Sleep(3000);
                                 })

                        // 3. 執行內容
                       .Execute(AttachmentSnowflake.Generate);

            var tag = attachment.IsImage ? "img" : "file";

            attachment.Id = newId;
            attachment.ExternalLink = string.Concat(attachment.Remote ? Setting.ATTACHMENT_URL : Setting.FORUM_URL, Setting.FORUM_ATTACHMENT_PATH, attachment.ExternalLink);
            attachment.BbCode = string.Concat("[", tag, "]", newId, "[/", tag, "]");
            attachment.CreationDate = DateTimeOffset.FromUnixTimeSeconds(attachment.Dateline);
            
            attachmentSb.AppendValueLine(attachment.Id, attachment.Size.ToCopyValue(), attachment.ExternalLink, attachment.Bucket.ToCopyValue(),
                                         attachment.DownloadCount, attachment.ProcessingState, attachment.DeleteStatus, attachment.IsPublic,
                                         attachment.StoragePath.ToCopyValue(), attachment.Name.ToCopyText(), attachment.ContentType.ToCopyValue(), attachment.ParentId.ToCopyValue(),
                                         attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);
            
            attachmentRelationSb.AppendValueLine(attachment.Pid,attachment.Aid,attachment.Id);
        }
        
        var attachmentTask = new Task(() => { FileHelper.WriteToFile($"{Setting.ATTACHMENT_PATH}/{tableNumber}", $"{startAid}.sql", ATTACHMENT_PREFIX, attachmentSb); });

        var attachmentRelationTask = new Task(() => { FileHelper.WriteToFile($"{ATTACHMENT_RELATION_PATH}/{tableNumber}", $"{startAid}.sql", ATTACHMENT_RELATION_PREFIX, attachmentRelationSb); });
            
        attachmentTask.Start();
        attachmentRelationTask.Start();

        await Task.WhenAll(attachmentTask, attachmentRelationTask);
    }
}