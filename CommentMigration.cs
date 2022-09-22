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
    private static readonly Dictionary<int, long> ArticleDic = RelationHelper.GetArticleDic();
    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<long, (long, string)> MemberDIc = RelationHelper.GetSimpleMemberDic();

    private const string EXTEND_DATA_RECOMMEND_COMMENT = "RecommendComment";
    private const string EXTEND_DATA_BOARD_ID = "BoardId";
    private const string COMMENT_JSON = "CommentJson";
    private const string ARTICLE_IGNORE = "ArticleIgnore";

    private const string COMMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}";
    private const string COMMENT_EXTEND_DATA_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}";
    private const string COMMENT_JSON_PATH = $"{Setting.INSERT_DATA_PATH}/{COMMENT_JSON}";
    private const string ARTICLE_IGNORE_PATH = $"{Setting.INSERT_DATA_PATH}/{ARTICLE_IGNORE}";
    private const string COMMENT_COMBINE_JSON_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}.json";


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

    private const string COPY_ARTICLE_IGNORE_PREFIX = $"COPY \"{ARTICLE_IGNORE}\" (\"Tid\",\"Reason\"" + Setting.COPY_SUFFIX;

    // private const string QUERY_COMMENT_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
    //                                             SELECT thread.fid,thread.tid,post.pid,post.authorid,post.dateline,post.first,post.status,post.comment,invisible AS IsDeleted,
    //                                             IF(`first`, thread.subject, null) AS Title,IF(`first`, '', post.message) AS Content,useip AS Ip,post.`position` -1 AS Sequence,
    //                                             likescore AS RelatedScore,postStick.dateline AS stickDateline
    //                                             FROM pre_forum_thread AS thread 
    //                                             LEFT JOIN `pre_forum_post{0}` AS post ON post.tid = thread.tid
    //                                             LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
    //                                             WHERE thread.posttableid = @postTableId 
    //                                             AND thread.dateline >= @Start AND thread.dateline < @End";

    private const string QUERY_COMMENT_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                                SELECT thread.fid,thread.tid,post.pid,post.authorid,post.dateline,post.first,post.status,post.comment,invisible AS IsDeleted,
                                                IF(`first`, thread.subject, null) AS Title,IF(`first`, '', post.message) AS Content,useip AS Ip,post.`position` -1 AS Sequence,
                                                likescore AS RelatedScore,postStick.dateline AS stickDateline
                                                FROM pre_forum_thread AS thread 
                                                LEFT JOIN `pre_forum_post{0}` AS post ON post.tid = thread.tid
                                                LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
                                                WHERE thread.posttableid = @postTableId
                                                AND thread.dateline >= @Start AND thread.dateline < @End";

    private readonly ISnowflake _snowflake;

    public CommentMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        RetryHelper.CreateCommentRetryTable();
        var (folderName, _) = RetryHelper.GetCommentRetry();
        var postTableIds = ArticleHelper.GetPostTableIds();
        var periods = PeriodHelper.GetPeriods(folderName);

        if (folderName != null)
            RetryHelper.RemoveFilesByDate(new[] { COMMENT_PATH, COMMENT_EXTEND_DATA_PATH, COMMENT_JSON_PATH, ARTICLE_IGNORE_PATH }, folderName);
        else
            RetryHelper.RemoveFiles(new[] { COMMENT_PATH, COMMENT_EXTEND_DATA_PATH, COMMENT_JSON_PATH,ARTICLE_IGNORE_PATH, COMMENT_COMBINE_JSON_PATH, });

        foreach (var period in periods)
        {
            await Parallel.ForEachAsync(postTableIds, CommonHelper.GetParallelOptions(cancellationToken), async (postTableId, token) =>
                                                                                                          {
                                                                                                              var posts = Array.Empty<CommentPost>();

                                                                                                              var sql = string.Format(QUERY_COMMENT_SQL, postTableId == 0 ? "" : $"_{postTableId}");

                                                                                                              try
                                                                                                              {
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
                                                                                                                           .ExecuteAsync(async () => { posts = (await cn.QueryAsync<CommentPost>(command)).ToArray(); });
                                                                                                                  }

                                                                                                                  if (!posts.Any())
                                                                                                                      return;

                                                                                                                  await ExecuteAsync(posts, postTableId, period, cancellationToken);
                                                                                                              }
                                                                                                              catch (Exception e)
                                                                                                              {
                                                                                                                  Console.WriteLine(e);
                                                                                                                  await File.AppendAllTextAsync($"{Setting.INSERT_DATA_PATH}/Error.txt", $"{period.FolderName}{Environment.NewLine}{e}", token);
                                                                                                                  RetryHelper.SetCommentRetry(period.FolderName, null, e.ToString());

                                                                                                                  throw;
                                                                                                              }
                                                                                                          });
        }

        await FileHelper.CombineMultipleFilesIntoSingleFileAsync(COMMENT_JSON_PATH,
                                                                 "*.json",
                                                                 COMMENT_COMBINE_JSON_PATH,
                                                                 cancellationToken);

        RetryHelper.DropCommentRetryTable();
    }

    private async Task ExecuteAsync(CommentPost[] posts, int postTableId, Period period, CancellationToken cancellationToken = default)
    {
        var commentSb = new StringBuilder();
        var commentJsonSb = new StringBuilder();
        var commentExtendDataSb = new StringBuilder();
        var ignoreSb = new StringBuilder();

        // var sw = new Stopwatch();
        // sw.Start();
        var attachPathDic = await RegexHelper.GetAttachFileNameDicAsync(RegexHelper.GetAttachmentGroups(posts), cancellationToken);

        // sw.Stop();
        // Console.WriteLine($"selectMany Time => {sw.ElapsedMilliseconds}ms");

        var removedTid = 0;
        var previousTid = 0;

        for (var i = 0; i < posts.Length; i++)
        {
            var post = posts[i];

            if (post.Tid == removedTid)
                continue;

            var id = ArticleDic.GetValueOrDefault(post.Tid);
            var boardId = BoardDic.GetValueOrDefault(post.Fid);
            var (memberId, memberName) = MemberDIc.GetValueOrDefault(post.Authorid);

            if (post.Tid != previousTid)
            {
                previousTid = post.Tid;

                var isDirty = true;
                var reason = "";
                
                //第一筆如果不是first或sequence!=0不處理
                if (post.First && post.Sequence == 0)
                {
                    (isDirty, reason) = IsDirty(id, boardId, memberId);
                }
                else
                {
                    var index = i;

                    while (++index < posts.Length)
                    {
                        var nextPost = posts[index];

                        if (nextPost.Tid != post.Tid)
                            break;

                        if (!nextPost.First || nextPost.Sequence != 0) continue;

                        var nextId = ArticleDic.GetValueOrDefault(nextPost.Tid);
                        var nextBoardId = BoardDic.GetValueOrDefault(nextPost.Fid);

                        var (nextMemberId, _) = MemberDIc.GetValueOrDefault(nextPost.Authorid);
                        
                        (isDirty, reason) = IsDirty(nextId, nextBoardId, nextMemberId);

                        // if (!(nextId == 0 || nextBoardId == 0 || nextMemberId == 0))
                        //     isDirty = false;

                        break;
                    }
                }

                if (isDirty)
                {
                    ignoreSb.AppendValueLine(post.Tid, reason);
                    removedTid = post.Tid;

                    continue;
                }
            }

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

        var commentTask = new Task(() => { WriteToFile($"{COMMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb); });
        var commentExtendDataTask = new Task(() => { WriteToFile($"{COMMENT_EXTEND_DATA_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_EXTEND_DATA_PREFIX, commentExtendDataSb); });
        var ignoreTask = new Task(() => { WriteToFile($"{ARTICLE_IGNORE_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_ARTICLE_IGNORE_PREFIX, ignoreSb); });
        var commentJsonTask = new Task(() => { WriteToFile($"{COMMENT_JSON_PATH}/{period.FolderName}", $"{postTableId}.json", "", commentJsonSb); });

        commentTask.Start();
        commentExtendDataTask.Start();
        ignoreTask.Start();
        commentJsonTask.Start();

        await Task.WhenAll(commentTask, commentExtendDataTask, commentJsonTask, ignoreTask);
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
        comment.Hierarchy = string.Concat(postResult.ArticleId, "/", commentId);
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
            WriteToFile($"{COMMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb);

            commentSb.Clear();
        }

        commentSb.AppendValueLine(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy, comment.SortingIndex,
                                  comment.Title != null ? comment.Title.ToCopyText() : comment.Title.ToCopyValue(), comment.Content.ToCopyText(),
                                  (int) comment.VisibleType, comment.Ip!, comment.Sequence, comment.RelatedScore, comment.ReplyCount, comment.LikeCount, comment.DislikeCount, comment.IsDeleted,
                                  comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);

        #region Es文件檔

        if (commentJsonSb.Length > maxStringBuilderLength)
        {
            WriteToFile($"{COMMENT_JSON_PATH}/{period.FolderName}", $"{postTableId}.json", "", commentJsonSb);

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
        }
        else
        {
            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(fullPath, string.Concat(copyPrefix, valueSb.ToString()));
            Console.WriteLine(fullPath);
        }
    }

    private static (bool isDirty, string reason) IsDirty(long id, long boarId, long memberId)
    {
        if (id == 0)
            return (true, "Article not exists");

        if (boarId == 0)
            return (true, "Board not exists");

        if (memberId == 0)
            return (true, "Member not exists");

        return (false, string.Empty);
    }
}