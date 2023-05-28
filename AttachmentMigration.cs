using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using Lctech.Attachment.Core.Domain.Entities;
using Lctech.Comment.Domain.Entities;
using Lctech.Jkf.Forum.Core.Models;
using Attachment = ForumDataMigration.Models.Attachment;

namespace ForumDataMigration;

public class AttachmentMigration
{
    private const string ATTACHMENT_PREFIX = $"COPY \"{nameof(Attachment)}\" " +
                                             $"(\"{nameof(Attachment.Id)}\",\"{nameof(Attachment.Size)}\",\"{nameof(Attachment.ExternalLink)}\",\"{nameof(Attachment.Bucket)}\"" +
                                             $",\"{nameof(Attachment.DownloadCount)}\",\"{nameof(Attachment.ProcessingState)}\",\"{nameof(Attachment.DeleteStatus)}\",\"{nameof(Attachment.IsPublic)}\"" +
                                             $",\"{nameof(Attachment.StoragePath)}\",\"{nameof(Attachment.Name)}\",\"{nameof(Attachment.ContentType)}\",\"{nameof(Attachment.Extension)}\",\"{nameof(Attachment.ParentId)}\"" +
                                             Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_ATTACHMENT_EXTEND_DATA_PREFIX = $"COPY \"{nameof(AttachmentExtendData)}\" (\"{nameof(AttachmentExtendData.Id)}\",\"{nameof(AttachmentExtendData.Key)}\",\"{nameof(AttachmentExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string ATTACHMENT_EXTEND_DATA_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}_ExtendData";

    public static async Task MigrationAsync(CancellationToken cancellationToken)
    {
        int? progressedMaxAid = null;

        // await using (var conn = new NpgsqlConnection(Setting.NEW_ATTACHMENT_CONNECTION))
        // {
        //     var startDate = DateTimeOffset.Parse(Setting.ATTACHMENT_START_DATE).AddHours(8).ToUniversalTime();
        //
        //     const string getMaxAIdSql = @"SELECT MIN(""Id"") FROM ""Attachment"" WHERE ""CreationDate"" >= @startDate";
        //     progressedMaxAid = conn.QueryFirst<int?>(getMaxAIdSql, new { startDate });
        //
        //     Console.WriteLine("progressedMaxAid:" + progressedMaxAid);
        // }

        if (!progressedMaxAid.HasValue)
            FileHelper.RemoveFiles(new[] { Setting.ATTACHMENT_PATH, ATTACHMENT_EXTEND_DATA_PATH });

        var tableNumbers = AttachmentHelper.TableNumbers;

        await Parallel.ForEachAsync(tableNumbers, CommonHelper.GetParallelOptions(cancellationToken), async (tableNumber, token) =>
                                                                                                      {
                                                                                                          var startAid = progressedMaxAid ?? 0;

                                                                                                          var sql = $@"SELECT a.tid,a.aid,a.uid,attachment AS ExternalLink,remote,isimage,
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

                                                                                                              Execute(attachments, tableNumber, attachments.First().Aid);

                                                                                                              startAid = attachments[^1].Aid + 1;
                                                                                                          }
                                                                                                      });
    }

    private static void Execute(IEnumerable<Attachment> attachments, int tableNumber, int startAid)
    {
        var attachmentSb = new StringBuilder();
        var attachmentExtendDataSb = new StringBuilder();

        foreach (var attachment in attachments)
        {
            attachment.Id = attachment.Aid * 10 + tableNumber;
            attachment.ExternalLink = string.Concat(attachment.Remote ? Setting.ATTACHMENT_URL : Setting.FORUM_URL, Setting.FORUM_ATTACHMENT_PATH, attachment.ExternalLink);
            attachment.CreationDate = DateTimeOffset.FromUnixTimeSeconds(attachment.Dateline);
            attachment.Extension = Path.GetExtension(attachment.Name)?.ToLower();

            attachmentSb.AppendValueLine(attachment.Id, attachment.Size.ToCopyValue(), attachment.ExternalLink, attachment.Bucket.ToCopyValue(),
                                         attachment.DownloadCount, attachment.ProcessingState, attachment.DeleteStatus, attachment.IsPublic,
                                         attachment.StoragePath.ToCopyValue(), attachment.Name.ToCopyText(), attachment.ContentType.ToCopyValue(), attachment.Extension.ToCopyText(), attachment.ParentId.ToCopyValue(),
                                         attachment.CreationDate, attachment.Uid, attachment.CreationDate, attachment.Uid, attachment.Version);

            attachmentExtendDataSb.AppendValueLine(attachment.Id, Constants.EXTEND_DATA_ARTICLE_ID, attachment.Tid,
                                                   attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);
        }

        FileHelper.WriteToFile($"{Setting.ATTACHMENT_PATH}/{tableNumber}", $"{startAid}.sql", ATTACHMENT_PREFIX, attachmentSb);
        FileHelper.WriteToFile($"{ATTACHMENT_EXTEND_DATA_PATH}/{tableNumber}", $"{startAid}.sql", COPY_ATTACHMENT_EXTEND_DATA_PREFIX, attachmentExtendDataSb);
    }
}