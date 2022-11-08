using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;
using Netcorext.Algorithms;

namespace ForumDataMigration.Helpers;

public static class AttachmentHelper
{
    public const string ATTACHMENT_PREFIX = $"COPY \"{nameof(Attachment)}\" " +
                                             $"(\"{nameof(Attachment.Id)}\",\"{nameof(Attachment.Size)}\",\"{nameof(Attachment.ExternalLink)}\"" +
                                             $",\"{nameof(Attachment.DownloadCount)}\",\"{nameof(Attachment.ProcessingState)}\",\"{nameof(Attachment.DeleteStatus)}\"" +
                                             Setting.COPY_ENTITY_SUFFIX;
    
    public static async Task<Dictionary<(int, int), Attachment>> GetAttachmentDicAsync(IGrouping<int, IEnumerable<int>>[] attachFileGroups, ISnowflake snowflake, CancellationToken cancellationToken)
    {
        // var sw = new Stopwatch();
        // sw.Start();
        var hasValue = false;

        int[]? GetValueByKey(int key)
        {
            var attachGroup = attachFileGroups.FirstOrDefault(x => x.Key == key)?.SelectMany(x => x).Distinct()?.ToArray();

            if (!attachGroup?.Any() ?? true)
                return new[] { -1 };

            hasValue = true;

            return attachGroup;
        }

        // sw.Stop();
        // Console.WriteLine($"get param Time => {sw.ElapsedMilliseconds}ms");

        // sw.Restart();

        await using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

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
            return new Dictionary<(int, int), Attachment>();

        var command = new CommandDefinition(@"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                                        SELECT aa.*,a.downloads AS DownloadCount FROM (
                                                        SELECT 1 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline 
                                                        FROM pre_forum_attachment_1 WHERE aid IN @Groups1
                                                        UNION ALL
                                                        SELECT 2 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_2 WHERE aid IN @Groups2
                                                        UNION ALL
                                                        SELECT 3 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_3 WHERE aid IN @Groups3
                                                        UNION ALL
                                                        SELECT 4 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_4 WHERE aid IN @Groups4
                                                        UNION ALL
                                                        SELECT 5 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_5 WHERE aid IN @Groups5
                                                        UNION ALL
                                                        SELECT 6 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_6 WHERE aid IN @Groups6
                                                        UNION ALL
                                                        SELECT 7 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_7 WHERE aid IN @Groups7
                                                        UNION ALL
                                                        SELECT 8 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_8 WHERE aid IN @Groups8
                                                        UNION ALL
                                                        SELECT 9 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_9 WHERE aid IN @Groups9
                                                        UNION ALL
                                                        SELECT 0 AS tableId,aid,attachment AS ExternalLink,remote,isimage,filename AS 'NAME',filesize AS Size,dateline
                                                        FROM pre_forum_attachment_0 WHERE aid IN @Groups0
                                                        ) aa
                                                        LEFT JOIN pre_forum_attachment a ON a.aid = aa.aid;", param, cancellationToken: cancellationToken);

        var attacheDic = (await cn.QueryAsync<Attachment>(command))
           .ToDictionary(x => (x.TableId, x.Aid), x =>
                                                  {
                                                      var newId = snowflake.Generate();
                                                      var tag = x.IsImage ? "img" : "file";
                                                      
                                                      x.Id = newId;
                                                      x.ExternalLink = string.Concat(x.Remote ? Setting.ATTACHMENT_URL : Setting.FORUM_URL, Setting.ATTACHMENT_PATH, x.ExternalLink);
                                                      x.BbCode = string.Concat("[", tag, "]", newId, "[/", tag, "]");
                                                      x.CreationDate = DateTimeOffset.FromUnixTimeSeconds(x.Dateline);

                                                      return x;
                                                  });

        // sw.Stop();
        // Console.WriteLine($"query attachment Time => {sw.ElapsedMilliseconds}ms");

        return attacheDic;
    }

    public static void AppendAttachmentValue(this StringBuilder attachmentSb,Attachment attachment)
    {
        attachmentSb.AppendValueLine(attachment.Id, attachment.Size.ToCopyValue(), attachment.ExternalLink,
                                     attachment.DownloadCount, attachment.ProcessingState, attachment.DeleteStatus,
                                     attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);
    }
}