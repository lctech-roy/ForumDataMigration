using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using Lctech.Attachment.Core.Domain.Entities;
using Lctech.Comment.Domain.Entities;
using Lctech.Jkf.Forum.Core.Models;
using Netcorext.Extensions.Commons;
using Npgsql;

namespace ForumDataMigration;

public class AttachmentCommentIdMigration
{
    private const string QUERY_COMMENT_ATTACHMENT_SQL = @"SELECT * FROM ""CommentAttachment""";
    
    private const string DELETE_ATTACHMENT_EXTEND_DATA_SQL = @"DELETE FROM ""AttachmentExtendData"" WHERE ""Key"" = 'ArticleId' AND ""Id"" = ANY(@attachmentIds)";
    
    private const string COPY_ATTACHMENT_EXTEND_DATA_PREFIX = $"COPY \"{nameof(AttachmentExtendData)}\" (\"{nameof(AttachmentExtendData.Id)}\",\"{nameof(AttachmentExtendData.Key)}\",\"{nameof(AttachmentExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;
    
    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        CommentAttachment[]? commentAttachments = null;

        await CommonHelper.WatchTimeAsync(nameof(QUERY_COMMENT_ATTACHMENT_SQL),
                                          async () =>
                                          {
                                              await using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

                                              var command = new CommandDefinition(QUERY_COMMENT_ATTACHMENT_SQL, cancellationToken: cancellationToken);

                                              commentAttachments = (await cn.QueryAsync<CommentAttachment>(command)).ToArray();
                                          });

        await CommonHelper.WatchTimeAsync(nameof(DELETE_ATTACHMENT_EXTEND_DATA_SQL),
                                          async () =>
                                          {
                                              var skip = 0;
                                              
                                              while (true)
                                              {
                                                  const int count = 1000;

                                                  var attachmentIds = commentAttachments!.Skip(skip).Take(count).Select(x => x.AttachmentId).ToArray();

                                                  if(attachmentIds.IsEmpty())
                                                      break;
                                                  
                                                  await using var cn = new NpgsqlConnection(Setting.NEW_ATTACHMENT_CONNECTION);

                                                  var command = new CommandDefinition(DELETE_ATTACHMENT_EXTEND_DATA_SQL,new {attachmentIds}, cancellationToken: cancellationToken);

                                                  var deleteCount = await cn.ExecuteAsync(command);

                                                  Console.WriteLine("Delete Count:" + deleteCount);
                                                  
                                                  skip += count;
                                              }
                                          });
        
        var attachmentExtendDataSb = new StringBuilder();

        foreach (var attachment in commentAttachments!)
        {
            attachmentExtendDataSb.AppendValueLine(attachment.AttachmentId, Constants.EXTEND_DATA_COMMENT_ID, attachment.Id,
                                                   attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);
        }
        FileHelper.WriteToFile($"{Setting.INSERT_DATA_PATH}", $"{nameof(Attachment)}_CommentId_ExtendData.sql", COPY_ATTACHMENT_EXTEND_DATA_PREFIX, attachmentExtendDataSb);
    }
}