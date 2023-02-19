using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using MySqlConnector;
using Netcorext.Algorithms;
using Npgsql;
using Polly;

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

    public static async Task<Dictionary<int, List<Attachment>>> GetAttachmentDicAsync(IGrouping<int, int>[] attachFileGroups, ISnowflake snowflake, CancellationToken cancellationToken)
    {
        // var sw = new Stopwatch();
        // sw.Start();
        var hasValue = false;

        int[]? GetValueByKey(int key)
        {
            var attachGroup = attachFileGroups.FirstOrDefault(x => x.Key == key)?.Select(x => x).Distinct()?.ToArray();

            if (!attachGroup?.Any() ?? true)
                return new[] { -1 };

            hasValue = true;

            return attachGroup;
        }

        // sw.Stop();
        // Console.WriteLine($"get param Time => {sw.ElapsedMilliseconds}ms");

        // sw.Restart();
        var param = new
                    {
                        Groups1 = GetValueByKey(1),
                        Groups2 = GetValueByKey(2),
                        Groups3 = GetValueByKey(3),
                        Groups4 = GetValueByKey(4),
                        Groups5 = GetValueByKey(5),
                        Groups6 = GetValueByKey(6),
                        Groups7 = GetValueByKey(7),
                        Groups8 = GetValueByKey(8),
                        Groups9 = GetValueByKey(9),
                        Groups0 = GetValueByKey(0),
                    };

        if (!hasValue)
            return new Dictionary<int, List<Attachment>>();

        var command = new CommandDefinition(@"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                                        SELECT aa.*,a.downloads AS DownloadCount FROM (
                                                        SELECT 1 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline 
                                                        FROM pre_forum_attachment_1 WHERE pid IN @Groups1
                                                        UNION ALL
                                                        SELECT 2 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_2 WHERE pid IN @Groups2
                                                        UNION ALL
                                                        SELECT 3 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_3 WHERE pid IN @Groups3
                                                        UNION ALL
                                                        SELECT 4 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_4 WHERE pid IN @Groups4
                                                        UNION ALL
                                                        SELECT 5 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_5 WHERE pid IN @Groups5
                                                        UNION ALL
                                                        SELECT 6 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_6 WHERE pid IN @Groups6
                                                        UNION ALL
                                                        SELECT 7 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_7 WHERE pid IN @Groups7
                                                        UNION ALL
                                                        SELECT 8 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_8 WHERE pid IN @Groups8
                                                        UNION ALL
                                                        SELECT 9 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_9 WHERE pid IN @Groups9
                                                        UNION ALL
                                                        SELECT 0 AS tableId,tid,pid,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_0 WHERE pid IN @Groups0
                                                        ) aa
                                                        LEFT JOIN pre_forum_attachment a ON a.aid = aa.aid;", param, cancellationToken: cancellationToken);

        await using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        try
        {
            var attacheDic = (await cn.QueryAsync<Attachment>(command))
                            .GroupBy(x => x.Pid).ToDictionary(x => x.Key, groups =>
                                                                          {
                                                                              IEnumerable<Attachment> attachments = groups;

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
                                                                                             .Execute(snowflake.Generate);

                                                                                  // var newId = snowflake.Generate();
                                                                                  var tag = attachment.IsImage ? "img" : "file";

                                                                                  attachment.Id = newId;
                                                                                  attachment.ExternalLink = string.Concat(attachment.Remote ? Setting.ATTACHMENT_URL : Setting.FORUM_URL, Setting.FORUM_ATTACHMENT_PATH, attachment.ExternalLink);
                                                                                  attachment.BbCode = string.Concat("[", tag, "]", newId, "[/", tag, "]");
                                                                                  attachment.CreationDate = DateTimeOffset.FromUnixTimeSeconds(attachment.Dateline);
                                                                              }

                                                                              return attachments.ToList();
                                                                          });

            return attacheDic;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            for (var i = 0; i < 10; i++)
            {
                Console.WriteLine(Environment.NewLine);
                Console.WriteLine("Group key:" + i);
                Console.WriteLine(Environment.NewLine);
                var paramStr = string.Join(',', GetValueByKey(i) ?? Array.Empty<int>());
                Console.WriteLine(paramStr);
                Console.WriteLine(Environment.NewLine);
            }

            throw;
        }
    }

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
        int? pid = null;

        if (Setting.TestTid != null)
        {
            const string getTableIdSql = $@"SELECT posttableid FROM pre_forum_thread where tid =@tid";
            using var sqlConnection = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);
            var tableId = sqlConnection.QueryFirst<int>(getTableIdSql, new { tid = Setting.TestTid });
            var getPIdSql = $@"SELECT pid FROM pre_forum_post_{tableId} where tid =@tid";
            pid = sqlConnection.QueryFirst<int>(getPIdSql, new { tid = Setting.TestTid });
        }

        var attachmentTableDic = TableNumbers.ToDictionary(x => x, x => new Dictionary<int, Dictionary<int, bool>>());

        CommonHelper.WatchTime(nameof(GetAttachmentTableDic)
                             , () =>
                               {
                                   Parallel.ForEach(TableNumbers, CommonHelper.GetParallelOptions(), (tableNumber) =>
                                                                                                     {
                                                                                                         var sql = $@"SELECT pid,aid,isimage FROM pre_forum_attachment_{tableNumber}";

                                                                                                         if (pid != null)
                                                                                                             sql += $" where pid={pid}";

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