using System.Text;
using Dapper;
using ForumDataMigration.Enums;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Enums;
using MySqlConnector;
using Netcorext.Algorithms;
using Polly;

namespace ForumDataMigration;

public class CommentMigration
{
    private static readonly List<Period> Periods = PeriodHelper.GetPeriods(2011, 6);
    private static readonly Dictionary<int, long> ArticleDic = RelationHelper.GetArticleDic();
    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<long, (long, string)> MemberDIc = RelationHelper.GetSimpleMemberDic();

    private const string EXTEND_DATA_RECOMMEND_COMMENT = "RecommendComment";
    private const string EXTEND_DATA_BOARD_ID = "BoardId";
    private const string COMMENT_JSON = "CommentJson";

    private static readonly string CommentEsIdPrefix = $"{{\"create\":{{ \"_id\": \"{nameof(DocumentType.Comment).ToLower()}-";
    private static readonly string CommentEsRootIdPrefix = $"\",\"routing\": \"{nameof(DocumentType.Thread).ToLower()}-";
    private static readonly string EsIdSuffix = $"\" }}}}";
    private static readonly string CommentRelationShipName = DocumentType.Comment.ToString().ToLower();
    private static readonly string CommentRelationShipParentPrefix = DocumentType.Thread.ToString().ToLower() + "-";


    private const string COPY_COMMENT_PREFIX = $"COPY \"{nameof(Comment)}\" " +
                                               $"(\"{nameof(Comment.Id)}\",\"{nameof(Comment.RootId)}\",\"{nameof(Comment.ParentId)}\",\"{nameof(Comment.Level)}\",\"{nameof(Comment.Hierarchy)}\"" +
                                               $",\"{nameof(Comment.SortingIndex)}\",\"{nameof(Comment.Title)}\",\"{nameof(Comment.Content)}\",\"{nameof(Comment.VisibleType)}\",\"{nameof(Comment.Ip)}\"" +
                                               $",\"{nameof(Comment.Sequence)}\",\"{nameof(Comment.RelatedScore)}\",\"{nameof(Comment.ReplyCount)}\",\"{nameof(Comment.LikeCount)}\"" +
                                               $",\"{nameof(Comment.DislikeCount)}\",\"{nameof(Comment.IsDeleted)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_COMMENT_EXTEND_DATA_PREFIX = $"COPY \"{nameof(CommentExtendData)}\" (\"{nameof(CommentExtendData.Id)}\",\"{nameof(CommentExtendData.Key)}\",\"{nameof(CommentExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;

    // private const string QUERY_COMMENT_SQL = $@"SELECT fid,post.tid,post.pid,authorid,post.dateline,first,status,comment,invisible AS IsDeleted,
    //                                             subject AS Title,IF(`first`, '', message) AS Content,useip AS Ip,post.`position` -1 AS Sequence,
    //                                             likescore AS RelatedScore,postStick.dateline AS stickDateline
    //                                             FROM `pre_forum_post{{0}}` AS post
    //                                             LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
    //                                             WHERE post.dateline >= @Start AND post.dateline < @End";
    
    private const string QUERY_COMMENT_SQL = $@"SELECT post.fid,post.tid,post.pid,post.authorid,post.dateline,post.first,post.status,post.comment,invisible AS IsDeleted,
                                                thread.subject AS Title,IF(`first`, '', message) AS Content,useip AS Ip,post.`position` -1 AS Sequence,
                                                likescore AS RelatedScore,postStick.dateline AS stickDateline
                                                FROM pre_forum_thread AS thread 
                                                LEFT JOIN `pre_forum_post{{0}}` AS post ON post.tid = thread.tid
                                                LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
                                                WHERE thread.posttableid = @postTableId AND post.tid IS NOT NULL
                                                AND thread.dateline >= @Start AND thread.dateline < @End";

    private readonly ISnowflake _snowflake;

    public CommentMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        var postTableIds = ArticleHelper.GetPostTableIds();

        foreach (var period in Periods)
        {
            await Parallel.ForEachAsync(postTableIds, CommonHelper.GetParallelOptions(cancellationToken), async (postTableId, token) =>
                                                                                                          {
                                                                                                              CommentPost[] posts = Array.Empty<CommentPost>();

                                                                                                              var sql = string.Format(QUERY_COMMENT_SQL, postTableId == 0 ? "" : $"_{postTableId}");

                                                                                                              await using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                                                                                              {
                                                                                                                  var command = new CommandDefinition(sql, new { postTableId, Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token);
                                                                                                                  
                                                                                                                  await Policy
                                                                                                                        // 1. 處理甚麼樣的例外
                                                                                                                       .Handle<EndOfStreamException>()
                                                                                                                        // 2. 重試策略，包含重試次數
                                                                                                                       .RetryAsync(5, (ex, retryCount) =>
                                                                                                                                 {
                                                                                                                                     Console.WriteLine($"發生錯誤：{ex.Message}，第 {retryCount} 次重試");
                                                                                                                                     Thread.Sleep(3000);
                                                                                                                                 })
                                                                                                                        // 3. 執行內容
                                                                                                                       .ExecuteAsync(async () =>
                                                                                                                                {
                                                                                                                                    posts = (await cn.QueryAsync<CommentPost>(command)).ToArray();
                                                                                                                                });
                                                                                                              }

                                                                                                              if (!posts.Any())
                                                                                                                  return;

                                                                                                              await ExecuteAsync(posts, postTableId, period, cancellationToken);
                                                                                                          });
        }

        await FileHelper.CombineMultipleFilesIntoSingleFileAsync($"{Setting.INSERT_DATA_PATH}/{COMMENT_JSON}",
                                                                 "*.json",
                                                                 $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}.json",
                                                                 cancellationToken);
    }

    private async Task ExecuteAsync(CommentPost[] posts, int postTableId, Period period, CancellationToken cancellationToken = default)
    {
        var commentSb = new StringBuilder();
        var commentJsonSb = new StringBuilder();
        var commentExtendDataSb = new StringBuilder();

        // var sw = new Stopwatch();
        // sw.Start();
        var attachPathDic = await RegexHelper.GetAttachFileNameDicAsync(RegexHelper.GetAttachmentGroups(posts), cancellationToken);

        // sw.Stop();
        // Console.WriteLine($"selectMany Time => {sw.ElapsedMilliseconds}ms");

        foreach (var post in posts)
        {
            var id = ArticleDic.GetValueOrDefault(post.Tid);
            var boardId = BoardDic.GetValueOrDefault(post.Fid);

            //髒資料放過他
            if (id == 0 || boardId == 0)
                continue;

            var (memberId, memberName) = MemberDIc.GetValueOrDefault(post.Authorid);

            if (memberId == 0)
                continue;

            var postResult = new CommentPostResult
                             {
                                 ArticleId = id,
                                 BoardId = boardId,
                                 MemberId = memberId,
                                 MemberName = memberName,
                                 CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline),
                                 CreateMilliseconds = Convert.ToInt64(post.Dateline) * 1000,
                                 Post = post,
                                 AttachPathDic = attachPathDic
                             };

            if (post.First && post.Sequence == 0) //文章
            {
                await SetCommentFirstAsync(postResult, commentSb, commentExtendDataSb, commentJsonSb, period, postTableId, cancellationToken);
            }
            else if (post.Sequence != 0) //留言
            {
                await SetCommentAsync(postResult, commentSb, commentExtendDataSb, commentJsonSb, period, postTableId, cancellationToken);
            }
        }

        var commentTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb); });
        var commentExtendDataTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_EXTEND_DATA_PREFIX, commentExtendDataSb); });
        var commentJsonTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{COMMENT_JSON}/{period.FolderName}", $"{postTableId}.json", "", commentJsonSb); });

        commentTask.Start();
        commentExtendDataTask.Start();
        commentJsonTask.Start();

        await Task.WhenAll(commentTask, commentExtendDataTask, commentJsonTask);
    }

    private static async Task SetCommentFirstAsync(CommentPostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder commentJsonSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        var comment = postResult.Post;
        comment.Id = postResult.ArticleId;
        comment.RootId = postResult.ArticleId;
        comment.Level = 1;
        comment.Hierarchy = postResult.ArticleId.ToString();
        comment.VisibleType = VisibleType.Public;
        comment.SortingIndex = postResult.CreateMilliseconds;
        comment.CreationDate = postResult.CreateDate;
        comment.CreatorId = postResult.MemberId;
        comment.ModificationDate = postResult.CreateDate;
        comment.ModifierId = postResult.MemberId;

        await AppendCommentSbAsync(postResult, comment, commentSb, commentJsonSb, period, postTableId, cancellationToken);

        commentExtendDataSb.AppendValueLine(postResult.ArticleId, EXTEND_DATA_BOARD_ID, postResult.BoardId,
                                            comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }

    private async Task SetCommentAsync(CommentPostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder commentJsonSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        var comment = postResult.Post;
        var commentId = _snowflake.Generate();
        comment.Id = commentId;
        comment.RootId = postResult.ArticleId;
        comment.ParentId = postResult.ArticleId;
        comment.Level = 2;
        comment.Hierarchy = $"{postResult.ArticleId}/{commentId}";
        comment.Content = RegexHelper.GetNewMessage(comment.Content, comment.Tid, postResult.AttachPathDic);
        comment.VisibleType = comment.Status == 1 ? VisibleType.Private : VisibleType.Public;
        comment.SortingIndex = postResult.CreateMilliseconds;
        comment.CreationDate = postResult.CreateDate;
        comment.CreatorId = postResult.MemberId;
        comment.ModificationDate = postResult.CreateDate;
        comment.ModifierId = postResult.MemberId;

        await AppendCommentSbAsync(postResult, comment, commentSb, commentJsonSb, period, postTableId, cancellationToken);

        if (comment.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(comment.StickDateline.Value);

            commentExtendDataSb.AppendValueLine(commentId, EXTEND_DATA_RECOMMEND_COMMENT, true,
                                                stickDate, 0, stickDate, 0, 0);
        }

        if (!comment.Comment) return;

        var postComments = (await CommentHelper.GetPostCommentsAsync(comment.Tid, comment.Pid, cancellationToken)).ToArray();

        if (!postComments.Any())
            return;
        
        var sequence = 1;

        foreach (var postComment in postComments)
        {
            var commentReplyId = _snowflake.Generate();
            var replyDate = DateTimeOffset.FromUnixTimeSeconds(postComment.Dateline);
            var (memberId, _) = MemberDIc.GetValueOrDefault(postComment.Authorid);

            postResult.ReplyMemberUid = postComment.Authorid;
            postResult.ReplyMemberName = postComment.Author ?? string.Empty;

            var commentReply = new Comment
                               {
                                   Id = commentReplyId,
                                   RootId = postResult.ArticleId,
                                   ParentId = commentId,
                                   Level = 3,
                                   Hierarchy = $"{postResult.ArticleId}/{commentId}/{commentReplyId}",
                                   Content = RegexHelper.GetNewMessage(postComment.Comment, comment.Tid, postResult.AttachPathDic),
                                   VisibleType = VisibleType.Public,
                                   Ip = postComment.Useip,
                                   Sequence = sequence++,
                                   SortingIndex = Convert.ToInt64(postComment.Dateline) * 1000,
                                   RelatedScore = 0,
                                   CreationDate = replyDate,
                                   CreatorId = memberId,
                                   ModificationDate = replyDate,
                                   ModifierId = memberId,
                               };

            await AppendCommentSbAsync(postResult, commentReply, commentSb, commentJsonSb, period, postTableId, cancellationToken);
        }
    }

    private static async Task AppendCommentSbAsync(CommentPostResult postResult, Comment comment, StringBuilder commentSb, StringBuilder commentJsonSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        const int maxStringBuilderLength = 60000;

        if (commentSb.Length > maxStringBuilderLength)
        {
            WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb);

            commentSb.Clear();
        }

        commentSb.AppendValueLine(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy, comment.SortingIndex,
                                  comment.Title.ToCopyText(), comment.Content.ToCopyText(), (int) comment.VisibleType, comment.Ip!, comment.Sequence,
                                  comment.RelatedScore, comment.ReplyCount, comment.LikeCount, comment.DislikeCount, comment.IsDeleted,
                                  comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);


        #region Es文件檔

        if (commentJsonSb.Length > maxStringBuilderLength)
        {
            WriteToFile($"{Setting.INSERT_DATA_PATH}/{COMMENT_JSON}/{period.FolderName}", $"{postTableId}.sql", "", commentJsonSb);

            commentJsonSb.Clear();
        }

        commentJsonSb.Append(CommentEsIdPrefix).Append(comment.Id).Append(CommentEsRootIdPrefix).Append(comment.RootId).AppendLine(EsIdSuffix);

        var commentDocument = SetCommentDocument(comment, postResult);

        var commentJson = await JsonHelper.GetJsonAsync(commentDocument, cancellationToken);

        commentJsonSb.AppendLine(commentJson);

        #endregion
    }

    private static Document SetCommentDocument(Comment comment, CommentPostResult postResult)
    {
        long? memberUid = comment.Level != 3 ? postResult.Post.Authorid : postResult.ReplyMemberUid;
        var memberName = comment.Level != 3 ? postResult.MemberName : postResult.ReplyMemberName;

        return new Document()
               {
                   //article part
                   Id = comment.Id,
                   Title = RegexHelper.CleanText(comment.Title),
                   Content = RegexHelper.CleanText(comment.Content) ?? string.Empty,
                   ReadPermission = 0,
                   RootId = comment.RootId,
                   ParentId = comment.ParentId,
                   Sequence = Convert.ToInt32(comment.Sequence),
                   SortingIndex = comment.SortingIndex,
                   Score = comment.RelatedScore,
                   Ip = comment.Ip,
                   PinType = 0,
                   PinPriority = 0,
                   VisibleType = (int) comment.VisibleType,
                   CreationDate = comment.CreationDate,
                   CreatorId = comment.CreatorId,
                   ModificationDate = comment.ModificationDate,
                   ModifierId = comment.ModifierId,

                   //document part
                   Type = DocumentType.Comment,
                   Deleted = comment.IsDeleted,
                   CreatorUid = memberUid,
                   CreatorName = memberName,
                   ModifierUid = memberUid,
                   ModifierName = memberName,
                   Relationship = new Relationship()
                                  {
                                      Name = CommentRelationShipName,
                                      Parent = CommentRelationShipParentPrefix + comment.RootId
                                  }
               };
    }


    private static void WriteToFile(string directoryPath, string fileName, string copyPrefix, StringBuilder valueSb)
    {
        if (valueSb.Length == 0)
            return;

        var fullPath = $"{directoryPath}/{fileName}";

        if (File.Exists(fullPath))
        {
            File.AppendAllText(fullPath, valueSb.ToString());
            Console.WriteLine($"Append {fullPath}");
        }
        else
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(fullPath, string.Concat(copyPrefix, valueSb.ToString()));
            Console.WriteLine(fullPath);
        }
    }
}