using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using MySqlConnector;
using Npgsql;

namespace ForumDataMigration.Helpers;

public static class AttachmentHelper
{
    public const string ATTACHMENT_PREFIX = $"COPY \"{nameof(Attachment)}\" " +
                                            $"(\"{nameof(Attachment.Id)}\",\"{nameof(Attachment.Size)}\",\"{nameof(Attachment.ExternalLink)}\",\"{nameof(Attachment.Bucket)}\"" +
                                            $",\"{nameof(Attachment.DownloadCount)}\",\"{nameof(Attachment.ProcessingState)}\",\"{nameof(Attachment.DeleteStatus)}\",\"{nameof(Attachment.IsPublic)}\"" +
                                            $",\"{nameof(Attachment.StoragePath)}\",\"{nameof(Attachment.Name)}\",\"{nameof(Attachment.ContentType)}\",\"{nameof(Attachment.ParentId)}\"" +
                                            Setting.COPY_ENTITY_SUFFIX;

    public static readonly int[] TableNumbers = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    private const string IMAGE_TAG = "img";
    private const string FILE_TAG = "file";

    public static void AppendAttachmentValue(this StringBuilder attachmentSb, Attachment attachment)
    {
        attachmentSb.AppendValueLine(attachment.Id, attachment.Size.ToCopyValue(), attachment.ExternalLink, attachment.Bucket.ToCopyValue(),
                                     attachment.DownloadCount, attachment.ProcessingState, attachment.DeleteStatus, attachment.IsPublic,
                                     attachment.StoragePath.ToCopyValue(), attachment.Name.ToCopyText(), attachment.ContentType.ToCopyValue(), attachment.ParentId.ToCopyValue(),
                                     attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);
    }

    public static (Dictionary<string, long>, Dictionary<long, List<Attachment>>) GetArtifactAttachmentDic()
    {
        var command = new CommandDefinition(@"SELECT ""Id"",""Name"",""ContentType"",""Size"",""IsPublic"",""Bucket"",""ParentId"",""CreationDate"",""ModificationDate"",""ObjectName"" FROM ""Artifact""");

        var pathIdDic = new Dictionary<string, long>();
        var attachmentDic = new Dictionary<long, List<Attachment>>();

        CommonHelper.WatchTime(nameof(GetArtifactAttachmentDic)
                             , () =>
                               {
                                   using var cn = new NpgsqlConnection(Setting.NEW_ARTIFACT_CONNECTION);

                                   var attachments = cn.Query<Attachment>(command);

                                   foreach (var attachment in attachments)
                                   {
                                       if (attachment.ParentId.HasValue)
                                           pathIdDic.Add(attachment.ObjectName, attachment.ParentId.Value);

                                       var parentId = attachment.ParentId ?? attachment.Id;

                                       if (!attachmentDic.ContainsKey(parentId))
                                           attachmentDic.Add(parentId, new List<Attachment> { attachment });
                                       else
                                           attachmentDic[parentId].Add(attachment);
                                   }
                               });

        return (pathIdDic, attachmentDic);
    }

    public static Dictionary<int, Dictionary<int, Dictionary<int, bool>>> GetAttachmentTableDic()
    {
        List<int>? pids = null;

        if (Setting.TestTid != null)
        {
            const string getTableIdSql = $@"SELECT posttableid FROM pre_forum_thread where tid =@tid";
            using var sqlConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
            var tableId = sqlConnection.QueryFirst<int>(getTableIdSql, new { tid = Setting.TestTid });
            var getPIdSql = $@"SELECT pid FROM pre_forum_post_{tableId} where tid =@tid";
            pids = sqlConnection.Query<int>(getPIdSql, new { tid = Setting.TestTid }).ToList();
        }

        var attachmentTableDic = TableNumbers.ToDictionary(x => x, x => new Dictionary<int, Dictionary<int, bool>>());

        CommonHelper.WatchTime(nameof(GetAttachmentTableDic)
                             , () =>
                               {
                                   Parallel.ForEach(TableNumbers, CommonHelper.GetParallelOptions(), (tableNumber) =>
                                                                                                     {
                                                                                                         var sql = $@"SELECT pid,aid,isimage FROM pre_forum_attachment_{tableNumber}";

                                                                                                         if (pids?.Any() ?? false)
                                                                                                             sql += $" where pid in ({string.Join(',', pids)})";

                                                                                                         using var sqlConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                                                         var attachmentDic = sqlConnection.Query<Attachment>(sql)
                                                                                                                                          .GroupBy(x => x.Pid)
                                                                                                                                          .ToDictionary(x => x.Key,
                                                                                                                                                        x => x.ToDictionary(y => y.Aid * 10 + tableNumber, y => y.IsImage));

                                                                                                         attachmentTableDic[tableNumber] = attachmentDic;
                                                                                                     });
                               });

        return attachmentTableDic;
    }

    public static string GetBbcode(int aid, bool isImage)
    {
        var tag = isImage ? IMAGE_TAG : FILE_TAG;

        return string.Concat("[", tag, "]", aid, "[/", tag, "]");
    }
}