using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Comment.Domain.Entities;
using Lctech.Comment.Enums;
using MySqlConnector;
using Netcorext.Algorithms;
using Polly;

namespace ForumDataMigration;

public class CommentMigration
{
    private static readonly HashSet<long> ArticleIdHash = RelationHelper.GetArticleIdHash();
    private static readonly HashSet<long> BoardIdHash = RelationHelper.GetBoardIdHash();

    private const string EXTEND_DATA_RECOMMEND_COMMENT = "RecommendComment";
    private const string EXTEND_DATA_BOARD_ID = "BoardId";
    private const string ARTICLE_IGNORE = "ArticleIgnore";

    private const string COMMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}";
    private const string COMMENT_EXTEND_DATA_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}";
    private const string ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Attachment)}_{nameof(Comment)}";
    private const string COMMENT_ATTACHMENT_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(CommentAttachment)}";
    private const string ARTICLE_IGNORE_PATH = $"{Setting.INSERT_DATA_PATH}/{ARTICLE_IGNORE}";

    private const string COPY_COMMENT_PREFIX = $"COPY \"{nameof(Comment)}\" " +
                                               $"(\"{nameof(Comment.Id)}\",\"{nameof(Comment.RootId)}\",\"{nameof(Comment.ParentId)}\",\"{nameof(Comment.Level)}\",\"{nameof(Comment.Hierarchy)}\"" +
                                               $",\"{nameof(Comment.SortingIndex)}\",\"{nameof(Comment.Title)}\",\"{nameof(Comment.Content)}\",\"{nameof(Comment.VisibleType)}\",\"{nameof(Comment.Ip)}\"" +
                                               $",\"{nameof(Comment.Sequence)}\",\"{nameof(Comment.RelatedScore)}\",\"{nameof(Comment.ReplyCount)}\",\"{nameof(Comment.LikeCount)}\"" +
                                               $",\"{nameof(Comment.DislikeCount)}\",\"{nameof(Comment.DeleteStatus)}\",\"{nameof(Comment.Status)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_COMMENT_EXTEND_DATA_PREFIX = $"COPY \"{nameof(CommentExtendData)}\" (\"{nameof(CommentExtendData.Id)}\",\"{nameof(CommentExtendData.Key)}\",\"{nameof(CommentExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_ARTICLE_IGNORE_PREFIX = $"COPY \"{ARTICLE_IGNORE}\" (\"Tid\",\"Reason\"" + Setting.COPY_SUFFIX;

    private const string COMMENT_ATTACHMENT_PREFIX = $"COPY \"{nameof(CommentAttachment)}\" " +
                                                     $"(\"{nameof(CommentAttachment.Id)}\",\"{nameof(CommentAttachment.AttachmentId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_COMMENT_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                                SELECT thread.fid,thread.tid,thread.replies,post.pid,post.authorid,post.dateline,post.first,post.status,post.comment,post.invisible,
                                                IF(`first`, thread.subject, null) AS Title,IF(`first`, '', post.message) AS Content,useip AS Ip,post.`position` -1 AS Sequence,
                                                likescore AS RelatedScore,postStick.dateline AS stickDateline
                                                FROM pre_forum_thread AS thread 
                                                LEFT JOIN `pre_forum_post{0}` AS post ON post.tid = thread.tid
                                                LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
                                                WHERE thread.posttableid = @postTableId AND thread.dateline >= @Start AND thread.dateline < @End AND thread.displayorder >= -3";

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

        if (folderName != null)
            Console.WriteLine("Retry on:" + folderName);

        if (Setting.TestTid != null)
            Console.WriteLine("Start Test:" + Setting.TestTid);

        Thread.Sleep(3000);

        //刪掉之前轉過的檔案
        if (folderName != null)
            FileHelper.RemoveFilesByDate(new[] { COMMENT_PATH, COMMENT_EXTEND_DATA_PATH, ATTACHMENT_PATH, COMMENT_ATTACHMENT_PATH, ARTICLE_IGNORE_PATH }, folderName);
        else
            FileHelper.RemoveFiles(new[] { COMMENT_PATH, COMMENT_EXTEND_DATA_PATH, ATTACHMENT_PATH, COMMENT_ATTACHMENT_PATH, ARTICLE_IGNORE_PATH });

        foreach (var period in periods)
        {
            try
            {
                await Parallel.ForEachAsync(postTableIds, CommonHelper.GetParallelOptions(cancellationToken), async (postTableId, token) =>
                                                                                                              {
                                                                                                                  var sql = string.Format(QUERY_COMMENT_SQL, postTableId == 0 ? "" : $"_{postTableId}");

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
                                                                                                                       .ExecuteAsync(async () =>
                                                                                                                                     {
                                                                                                                                         if (Setting.TestTid != null)
                                                                                                                                             sql += $" AND thread.tid = {Setting.TestTid}";

                                                                                                                                         await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                                                                                         var command = new CommandDefinition(sql, new { postTableId, Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token);
                                                                                                                                         var posts = (await cn.QueryAsync<CommentPost>(command)).ToArray();

                                                                                                                                         if (!posts.Any())
                                                                                                                                             return;

                                                                                                                                         await ExecuteAsync(posts, postTableId, period, cancellationToken);
                                                                                                                                     });
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

        var removedTid = 0;
        var previousTid = 0;

        for (var i = 0; i < posts.Length; i++)
        {
            var post = posts[i];

            if (!BoardIdHash.Contains(post.Fid))
                continue;

            if (!ArticleIdHash.Contains(post.Tid))
                continue;

            if (post.Tid == removedTid)
                continue;

            if (post.Tid != previousTid)
            {
                previousTid = post.Tid;

                var isDirty = true;
                var reason = "";

                //第一筆如果不是first或sequence!=0不處理
                if (post.First && post.Sequence == 0)
                {
                    (isDirty, reason) = IsDirty(post.Tid, post.Fid);
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

                        var nextId = nextPost.Tid;
                        var nextBoardId = nextPost.Fid;

                        (isDirty, reason) = IsDirty(nextId, nextBoardId);

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

            post.CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline);
            post.CreateMilliseconds = Convert.ToInt64(post.Dateline) * 1000;

            if (post is { First: true, Sequence: 0 }) //文章
            {
                SetCommentFirst(post, commentSb, commentExtendDataSb, period, postTableId);
            }
            else if (post.Sequence != 0) //留言
            {
                await SetCommentAsync(post, commentSb, commentExtendDataSb, attachmentSb, commentAttachmentSb, period, postTableId, cancellationToken);
            }
        }

        var commentTask = new Task(() => { FileHelper.WriteToFile($"{COMMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb); });
        var commentExtendDataTask = new Task(() => { FileHelper.WriteToFile($"{COMMENT_EXTEND_DATA_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_EXTEND_DATA_PREFIX, commentExtendDataSb); });
        var ignoreTask = new Task(() => { FileHelper.WriteToFile($"{ARTICLE_IGNORE_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_ARTICLE_IGNORE_PREFIX, ignoreSb); });
        var attachmentTask = new Task(() => { FileHelper.WriteToFile($"{ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", AttachmentHelper.ATTACHMENT_PREFIX, attachmentSb); });
        var commentAttachmentTask = new Task(() => { FileHelper.WriteToFile($"{COMMENT_ATTACHMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COMMENT_ATTACHMENT_PREFIX, commentAttachmentSb); });

        commentTask.Start();
        commentExtendDataTask.Start();
        ignoreTask.Start();
        attachmentTask.Start();
        commentAttachmentTask.Start();

        await Task.WhenAll(commentTask, commentExtendDataTask, ignoreTask, attachmentTask, commentAttachmentTask);
    }

    private static void SetCommentFirst(CommentPost post, StringBuilder commentSb, StringBuilder commentExtendDataSb, Period period, int postTableId)
    {
        var comment = new Comment
                      {
                          Id = post.Tid,
                          RootId = post.Tid,
                          Level = 1,
                          Sequence = post.Sequence,
                          Ip = post.Ip,
                          RelatedScore = post.RelatedScore,
                          Title = post.Title,
                          Content = string.Empty,
                          Hierarchy = post.Tid.ToString(),
                          ReplyCount = post.Replies,
                          SortingIndex = post.CreateMilliseconds,
                          CreationDate = post.CreateDate,
                          CreatorId = post.Authorid,
                          ModificationDate = post.CreateDate,
                          ModifierId = post.Authorid,
                          VisibleType = VisibleType.Public,
                          DeleteStatus = post.Invisible ? DeleteStatus.Deleted : DeleteStatus.None,
                          Status = CommentStatus.NoneCommentStatus
                      };

        AppendCommentSb(comment, commentSb, period, postTableId);

        commentExtendDataSb.AppendValueLine(post.Tid, EXTEND_DATA_BOARD_ID, post.Fid,
                                            comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }

    private async Task SetCommentAsync(CommentPost post, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder attachmentSb, StringBuilder commentAttachmentSb, Period period, int postTableId,
                                       CancellationToken cancellationToken)
    {
        var newPid = post.Pid * 10;

        var comment = new Comment
                      {
                          Id = newPid,
                          RootId = post.Tid,
                          ParentId = post.Tid,
                          Level = 2,
                          Hierarchy = string.Concat(post.Tid, "/", newPid),
                          Sequence = post.Sequence,
                          Ip = post.Ip,
                          RelatedScore = post.RelatedScore,
                          Content = RegexHelper.GetNewMessage(post.Content, post.Tid % 10, post.Pid, post.Pid, post.Authorid, post.CreateDate,
                                                              attachmentSb, commentAttachmentSb, true),
                          SortingIndex = post.CreateMilliseconds,
                          CreationDate = post.CreateDate,
                          CreatorId = post.Authorid,
                          ModificationDate = post.CreateDate,
                          ModifierId = post.Authorid,
                          VisibleType = post.Status == 1 ? VisibleType.Hidden : VisibleType.Public,
                          DeleteStatus = post.Invisible ? DeleteStatus.Deleted : DeleteStatus.None,
                          Status = CommentStatus.NoneCommentStatus
                      };

        //連載
        if (post.Fid is 228 or 209)
            comment.Title = SerializeHelper.GetTitle(post.Content);

        PreForumPostcomment[]? postComments = null;

        if (post.Comment)
            postComments = (await CommentHelper.GetPostCommentsAsync(post.Tid, post.Pid, cancellationToken)).ToArray();

        comment.ReplyCount = postComments?.Length ?? 0;

        AppendCommentSb(comment, commentSb, period, postTableId);

        if (post.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(post.StickDateline.Value);

            commentExtendDataSb.AppendValueLine(newPid, EXTEND_DATA_RECOMMEND_COMMENT, true,
                                                stickDate, 0, stickDate, 0, 0);
        }

        if (postComments == null || !postComments.Any())
            return;

        var sequence = 1;

        foreach (var postComment in postComments)
        {
            var commentReplyId = _snowflake.Generate();
            var replyDate = DateTimeOffset.FromUnixTimeSeconds(postComment.Dateline);
            var memberId = postComment.Authorid;

            var commentReply = new Comment
                               {
                                   Id = commentReplyId,
                                   RootId = post.Tid,
                                   ParentId = newPid,
                                   Level = 3,
                                   Hierarchy = $"{post.Tid}/{newPid}/{commentReplyId}",
                                   Content = RegexHelper.GetNewMessage(postComment.Comment, post.Tid % 10, post.Pid, null, postComment.Authorid, replyDate,
                                                                       attachmentSb, commentAttachmentSb, true),
                                   VisibleType = VisibleType.Public,
                                   Ip = postComment.Useip,
                                   Sequence = sequence++,
                                   SortingIndex = Convert.ToInt64(postComment.Dateline) * 1000,
                                   RelatedScore = 0,
                                   CreationDate = replyDate,
                                   CreatorId = memberId,
                                   ModificationDate = replyDate,
                                   ModifierId = memberId,
                                   Status = CommentStatus.NoneCommentStatus
                               };

            AppendCommentSb(commentReply, commentSb, period, postTableId);
        }
    }

    private static void AppendCommentSb(Comment comment, StringBuilder commentSb, Period period, int postTableId)
    {
        const int maxStringBuilderLength = 60000;

        if (commentSb.Length > maxStringBuilderLength)
        {
            FileHelper.WriteToFile($"{COMMENT_PATH}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb);
        }

        commentSb.AppendValueLine(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy,
                                  comment.SortingIndex, comment.Title != null ? comment.Title.ToCopyText() : comment.Title.ToCopyValue(),
                                  comment.Content.ToCopyText(), (int) comment.VisibleType, comment.Ip!, comment.Sequence, comment.RelatedScore,
                                  comment.ReplyCount, comment.LikeCount, comment.DislikeCount, (int) comment.DeleteStatus, (int) comment.Status,
                                  comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }

    private static (bool isDirty, string reason) IsDirty(long id, long boarId)
    {
        if (id == 0)
            return (true, "Article not exists");

        if (boarId == 0)
            return (true, "Board not exists");

        return (false, string.Empty);
    }
}