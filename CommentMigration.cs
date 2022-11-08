using System.Text;
using Dapper;
using ForumDataMigration.Enums;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Core.Domain;
using Lctech.Jkf.Forum.Enums;
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
    private const string ARTICLE_IGNORE = "ArticleIgnore";

    private const string COMMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}";
    private const string COMMENT_EXTEND_DATA_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}";
    private const string ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}";
    private const string COMMENT_ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentAttachment)}";
    private const string ARTICLE_IGNORE_PATH = $"{Setting.INSERT_DATA_PATH}/{ARTICLE_IGNORE}";

    private const string COPY_COMMENT_PREFIX = $"COPY \"{nameof(Comment)}\" " +
                                               $"(\"{nameof(Comment.Id)}\",\"{nameof(Comment.RootId)}\",\"{nameof(Comment.ParentId)}\",\"{nameof(Comment.Level)}\",\"{nameof(Comment.Hierarchy)}\"" +
                                               $",\"{nameof(Comment.SortingIndex)}\",\"{nameof(Comment.Title)}\",\"{nameof(Comment.Content)}\",\"{nameof(Comment.VisibleType)}\",\"{nameof(Comment.Ip)}\"" +
                                               $",\"{nameof(Comment.Sequence)}\",\"{nameof(Comment.RelatedScore)}\",\"{nameof(Comment.ReplyCount)}\",\"{nameof(Comment.LikeCount)}\"" +
                                               $",\"{nameof(Comment.DislikeCount)}\",\"{nameof(Comment.DeleteStatus)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_COMMENT_EXTEND_DATA_PREFIX = $"COPY \"{nameof(CommentExtendData)}\" (\"{nameof(CommentExtendData.Id)}\",\"{nameof(CommentExtendData.Key)}\",\"{nameof(CommentExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_ARTICLE_IGNORE_PREFIX = $"COPY \"{ARTICLE_IGNORE}\" (\"Tid\",\"Reason\"" + Setting.COPY_SUFFIX;

    private const string COMMENT_ATTACHMENT_PREFIX = $"COPY \"{nameof(CommentAttachment)}\" " +
                                                     $"(\"{nameof(CommentAttachment.Id)}\",\"{nameof(CommentAttachment.AttachmentId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_COMMENT_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                                SELECT thread.fid,thread.tid,thread.replies,post.pid,post.authorid,post.dateline,post.first,post.status,post.comment,invisible,
                                                IF(`first`, thread.subject, null) AS Title,IF(`first`, '', post.message) AS Content,useip AS Ip,post.`position` -1 AS Sequence,
                                                likescore AS RelatedScore,postStick.dateline AS stickDateline
                                                FROM pre_forum_thread AS thread 
                                                LEFT JOIN `pre_forum_post{0}` AS post ON post.tid = thread.tid
                                                LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
                                                WHERE thread.posttableid = @postTableId AND thread.dateline >= @Start AND thread.dateline < @End AND thread.displayorder <> -4";

    private readonly ISnowflake _snowflake;
    private static readonly ISnowflake AttachmentSnowflake = new SnowflakeJavaScriptSafeInteger(2);

    public CommentMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        RetryHelper.CreateCommentRetryTable();

        if (Setting.USE_UPDATED_DATE)
            RetryHelper.SetCommentRetry(RetryHelper.GetEarliestCreateDateStr(), null, string.Empty);

        var folderName = RetryHelper.GetCommentRetryDateStr();
        var postTableIds = ArticleHelper.GetPostTableIds();
        var periods = PeriodHelper.GetPeriods(folderName);

        //刪掉之前轉過的檔案
        if (folderName != null)
            RetryHelper.RemoveFilesByDate(new[] { COMMENT_PATH, COMMENT_EXTEND_DATA_PATH, ATTACHMENT_PATH, COMMENT_ATTACHMENT_PATH, ARTICLE_IGNORE_PATH }, folderName);
        else
            RetryHelper.RemoveFiles(new[] { COMMENT_PATH, COMMENT_EXTEND_DATA_PATH, ATTACHMENT_PATH, COMMENT_ATTACHMENT_PATH, ARTICLE_IGNORE_PATH });

        foreach (var period in periods)
        {
            try
            {
                await Parallel.ForEachAsync(postTableIds, CommonHelper.GetParallelOptions(cancellationToken), async (postTableId, token) =>
                                                                                                              {
                                                                                                                  var posts = Array.Empty<CommentPost>();

                                                                                                                  var sql = string.Format(QUERY_COMMENT_SQL, postTableId == 0 ? "" : $"_{postTableId}");


                                                                                                                  await using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                                                                                                  {
                                                                                                                      var command = new CommandDefinition(sql, new { postTableId, Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token);

                                                                                                                      await Policy

                                                                                                                            // 1. 處理甚麼樣的例外
                                                                                                                           .Handle<EndOfStreamException>()
                                                                                                                           .Or<ArgumentOutOfRangeException>()

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
                                                                                                              });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync($"{Setting.INSERT_DATA_PATH}/Error.txt", $"{period.FolderName}{Environment.NewLine}{e}", cancellationToken);
                RetryHelper.SetCommentRetry(period.FolderName, null, e.ToString());

                throw;
            }
        }
        
        // RetryHelper.DropCommentRetryTable();
    }

    private async Task ExecuteAsync(CommentPost[] posts, int postTableId, Period period, CancellationToken cancellationToken = default)
    {
        var commentSb = new StringBuilder();
        var commentExtendDataSb = new StringBuilder();
        var attachmentSb = new StringBuilder();
        var commentAttachmentSb = new StringBuilder();
        var ignoreSb = new StringBuilder();

        // var sw = new Stopwatch();
        // sw.Start();
        var attachmentDic = await AttachmentHelper.GetAttachmentDicAsync(RegexHelper.GetAttachmentGroups(posts), AttachmentSnowflake, cancellationToken);

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
                                 AttachmentDic = attachmentDic
                             };

            if (post.First && post.Sequence == 0) //文章
            {
                SetCommentFirst(postResult, commentSb, commentExtendDataSb, period, postTableId);
            }
            else if (post.Sequence != 0) //留言
            {
                await SetCommentAsync(postResult, commentSb, commentExtendDataSb, attachmentSb, commentAttachmentSb, period, postTableId, cancellationToken);
            }
        }

        var commentTask = new Task(() => { WriteToFile($"{COMMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb); });
        var commentExtendDataTask = new Task(() => { WriteToFile($"{COMMENT_EXTEND_DATA_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_EXTEND_DATA_PREFIX, commentExtendDataSb); });
        var ignoreTask = new Task(() => { WriteToFile($"{ARTICLE_IGNORE_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_ARTICLE_IGNORE_PREFIX, ignoreSb); });
        var attachmentTask = new Task(() => { WriteToFile($"{ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", AttachmentHelper.ATTACHMENT_PREFIX, attachmentSb); });
        var articleAttachmentTask = new Task(() => { WriteToFile($"{COMMENT_ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COMMENT_ATTACHMENT_PREFIX, commentAttachmentSb); });

        commentTask.Start();
        commentExtendDataTask.Start();
        ignoreTask.Start();
        attachmentTask.Start();
        articleAttachmentTask.Start();

        await Task.WhenAll(commentTask, commentExtendDataTask, ignoreTask, attachmentTask, articleAttachmentTask);
    }

    private static void SetCommentFirst(CommentPostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, Period period, int postTableId)
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
        comment.DeleteStatus = comment.Invisible ? DeleteStatus.Deleted : DeleteStatus.None;
        comment.ReplyCount = postResult.Post.ReplyCount;

         AppendCommentSb(comment, commentSb, period, postTableId);

        commentExtendDataSb.AppendValueLine(postResult.ArticleId, EXTEND_DATA_BOARD_ID, postResult.BoardId,
                                            comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }

    private async Task SetCommentAsync(CommentPostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder attachmentSb, StringBuilder commentAttachmentSb, Period period, int postTableId,
                                       CancellationToken cancellationToken)
    {
        var comment = postResult.Post;
        var commentId = _snowflake.Generate();
        comment.Id = commentId;
        comment.RootId = postResult.ArticleId;
        comment.ParentId = postResult.ArticleId;
        comment.Level = 2;
        comment.Hierarchy = string.Concat(postResult.ArticleId, "/", commentId);
        comment.Content = RegexHelper.GetNewMessage(comment.Content, comment.Tid, commentId, postResult.MemberId, postResult.AttachmentDic, attachmentSb, commentAttachmentSb);
        comment.VisibleType = comment.Status == 1 ? VisibleType.Hidden : VisibleType.Public;
        comment.SortingIndex = postResult.CreateMilliseconds;
        comment.CreationDate = postResult.CreateDate;
        comment.CreatorId = postResult.MemberId;
        comment.ModificationDate = postResult.CreateDate;
        comment.ModifierId = postResult.MemberId;
        comment.DeleteStatus = comment.Invisible ? DeleteStatus.Deleted : DeleteStatus.None;

        PreForumPostcomment[]? postComments = null;

        if (comment.Comment)
            postComments = (await CommentHelper.GetPostCommentsAsync(comment.Tid, comment.Pid, cancellationToken)).ToArray();

        comment.ReplyCount = postComments?.Length ?? 0;
        
        AppendCommentSb(comment, commentSb, period, postTableId);

        if (comment.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(comment.StickDateline.Value);

            commentExtendDataSb.AppendValueLine(commentId, EXTEND_DATA_RECOMMEND_COMMENT, true,
                                                stickDate, 0, stickDate, 0, 0);
        }

        if (postComments == null || !postComments.Any())
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
                                   Content = RegexHelper.GetNewMessage(postComment.Comment, comment.Tid, commentReplyId, memberId, postResult.AttachmentDic, attachmentSb, commentAttachmentSb),
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
            
            AppendCommentSb(commentReply, commentSb, period, postTableId);
        }
    }

    private static void AppendCommentSb(Comment comment, StringBuilder commentSb, Period period, int postTableId)
    {
        const int maxStringBuilderLength = 60000;

        if (commentSb.Length > maxStringBuilderLength)
        {
            WriteToFile($"{COMMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb);

            commentSb.Clear();
        }

        commentSb.AppendValueLine(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy, comment.SortingIndex,
                                  comment.Title != null ? comment.Title.ToCopyText() : comment.Title.ToCopyValue(), comment.Content.ToCopyText(),
                                  (int) comment.VisibleType, comment.Ip!, comment.Sequence, comment.RelatedScore, comment.ReplyCount, comment.LikeCount, comment.DislikeCount, (int) comment.DeleteStatus,
                                  comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
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