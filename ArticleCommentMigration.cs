using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Lctech.Jkf.Domain.Enums;
using MySqlConnector;
using Netcorext.Algorithms;

namespace ForumDataMigration;

public partial class ArticleCommentMigration
{
    private readonly ISnowflake _snowflake;

    public ArticleCommentMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        foreach (var period in Periods)
        {
            await Parallel.ForEachAsync(PostTableIds, cancellationToken,
                                        async (postTableId, token) =>
                                        {
                                            List<Post> posts;

                                            await using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                            {
                                                var postTableSuffix = postTableId != 0 ? $"_{postTableId}" : "";

                                                var sql = string.Format(QUERY_ARTICLE_COMMENT_SQL, postTableSuffix);

                                                var command = new CommandDefinition(sql, new { Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token);

                                                posts = (await cn.QueryAsync<Post>(command)).ToList();
                                            }

                                            if (!posts.Any())
                                                return;

                                            await ExecuteAsync(posts, postTableId, period, cancellationToken);
                                        });
        }
    }

    private async Task ExecuteAsync(List<Post> posts, int postTableId, Period period, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var coverSb = new StringBuilder();
        var commentSb = new StringBuilder();
        var commentExtendDataSb = new StringBuilder();
        var warningSb = new StringBuilder();
        var rewardSb = new StringBuilder();

        var dic = await RelationHelper.GetArticleIdDicAsync(posts.Select(x => x.Tid).Distinct().ToArray(), cancellationToken);
        var simpleMemberDic = await RelationHelper.GetSimpleMemberDicAsync(posts.Select(x => x.Authorid.ToString()).Distinct().ToArray(), cancellationToken);
        var lastPosterNames = posts.Select(x => x.Lastposter).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();

        var memberDisplayNameDic = new Dictionary<string, long>();

        if (lastPosterNames.Any())
            memberDisplayNameDic = await RelationHelper.GetMembersDisplayNameDicAsync(lastPosterNames!, cancellationToken);

        var rewardDic = new Dictionary<int, ArticleReward>(); //有解決的懸賞文章要暫存

        foreach (var post in posts)
        {
            //髒資料放過他
            if (!dic.ContainsKey(post.Tid) || !BoardDic.ContainsKey(post.Fid))
                continue;

            var postResult = new PostResult
                             {
                                 ArticleId = dic[post.Tid],
                                 BoardId = BoardDic[post.Fid],
                                 MemberId = simpleMemberDic.ContainsKey(Convert.ToInt32(post.Authorid)) ? simpleMemberDic[Convert.ToInt32(post.Authorid)].Item1 : 0,
                                 CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline),
                                 CreateMilliseconds = Convert.ToInt64(post.Dateline) * 1000,
                                 MemberDisplayNameDic = memberDisplayNameDic,
                                 SimpleMemberDic = simpleMemberDic,
                                 Post = post
                             };

            if (post.First) //文章
            {
                SetArticle(postResult, sb, coverSb);
                SetArticleReward(postResult, rewardSb, rewardDic);
                //SetArticleWarning(postResult,warningSb);
                commentSb = await SetCommentFirstAsync(postResult, commentSb, commentExtendDataSb, period, postTableId, cancellationToken);
            }
            else if (post.Position != 1) //留言
            {
                var commentId = _snowflake.Generate();
                SetArticleRewardSolved(postResult, rewardSb, rewardDic, commentId);
                commentSb = await SetCommentAsync(postResult, commentSb, commentExtendDataSb, period, postTableId, commentId, cancellationToken);
            }
        }

        var task = WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(Article)}/{period.FolderName}", $"{postTableId}.sql", COPY_PREFIX, sb, cancellationToken);
        var coverTask = WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}/{period.FolderName}", $"{postTableId}.sql", COVER_RELATION_PREFIX, coverSb, cancellationToken);
        var rewardTask = WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}/{period.FolderName}", $"{postTableId}.sql", COPY_REWARD_PREFIX, rewardSb, cancellationToken);
        var warningTask = WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(Warning)}/{period.FolderName}", $"{postTableId}.sql", COPY_WARNING_PREFIX, warningSb, cancellationToken);
        var commentTask = WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb, cancellationToken);
        var commentExtendDataTask = WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_EXTEND_DATA_PREFIX, commentExtendDataSb, cancellationToken);

        await Task.WhenAll(task, coverTask, rewardTask, warningTask, commentTask, commentExtendDataTask);
    }

    private static void SetArticle(PostResult postResult, StringBuilder sb, StringBuilder coverSb)
    {
        var post = postResult.Post;

        var isScheduling = post.PostTime < post.Dateline;
        var highlightInt = post.Highlight % 10; //只要取個位數
        var read = ReadDic.ContainsKey(post.Tid) ? ReadDic[post.Tid] : null;
        var imageCount = BbCodeImageRegex.Matches(post.Message).Count;
        var videoCount = BbCodeVideoRegex.Matches(post.Message).Count;

        var article = new Article
                      {
                          Id = postResult.ArticleId,
                          Status = post.Displayorder == -1 ? ArticleStatus.Deleted :
                                   post.Displayorder == -2 ? ArticleStatus.Pending :
                                   post.Displayorder == -3 ? ArticleStatus.Hide : //待確認
                                   post.Displayorder == -4 ? ArticleStatus.Hide :
                                   isScheduling ? ArticleStatus.Scheduling :
                                   ArticleStatus.Published,
                          Type = post.Special switch
                                 {
                                     1 => ArticleType.Vote,
                                     2 => ArticleType.Diversion,
                                     3 => ArticleType.Reward,
                                     _ => ArticleType.Article
                                 },
                          ContentType = imageCount switch
                                        {
                                            0 when videoCount == 0 => ContentType.PaintText,
                                            > 0 when videoCount > 0 => ContentType.Complex,
                                            _ => imageCount > 0 ? ContentType.Image : ContentType.Video
                                        },
                          PinType = post.Displayorder switch
                                    {
                                        1 => PinType.Board,
                                        2 => PinType.Area,
                                        3 => PinType.Global,
                                        _ => PinType.None
                                    },
                          VisibleType = isScheduling ? VisibleType.Private : VisibleType.Public,
                          Title = post.Subject,
                          Content = RegexHelper.GetNewMessage(post.Message, post.Tid),
                          ViewCount = post.Views,
                          ReplyCount = post.Replies,
                          BoardId = postResult.BoardId,
                          CategoryId = CategoryDic.ContainsKey(post.Typeid) ? CategoryDic[post.Typeid] : 0,
                          SortingIndex = postResult.CreateMilliseconds,
                          LastReplyDate = post.Lastpost.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Lastpost.Value) : null,
                          LastReplierId = !string.IsNullOrEmpty(post.Lastposter) && postResult.MemberDisplayNameDic.ContainsKey(post.Lastposter) ? postResult.MemberDisplayNameDic[post.Lastposter] : null,
                          PinPriority = post.Displayorder,
                          Cover = SetArticleCoverRelation(postResult, coverSb)?.Id,
                          Tag = post.Tags.ToNewTags(),
                          RatingCount = post.Ratetimes ?? 0,
                          ShareCount = post.Sharetimes,
                          ImageCount = imageCount,
                          VideoCount = videoCount,
                          DonatePoint = 0,
                          Highlight = post.Highlight != 0,
                          HighlightColor = ColorDic.ContainsKey(highlightInt) ? ColorDic[highlightInt] : null,
                          Recommend = post.Digest,
                          ReadPermission = post.Readperm,
                          CommentDisabled = post.Closed == 1,
                          CommentVisibleType = post.Status == 34 ? VisibleType.Private : VisibleType.Public,
                          LikeCount = post.ThankCount ?? 0,
                          Ip = post.Useip,
                          Price = post.Price,
                          AuditorId = read?.ReadUid,
                          AuditFloor = read?.ReadFloor,
                          SchedulePublishDate = post.PostTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.PostTime.Value) : null,
                          HideExpirationDate = BbCodeHideTagRegex.IsMatch(post.Message) ? postResult.CreateDate.AddDays(CommonSetting.HideExpirationDay) : null,
                          PinExpirationDate = ModDic.ContainsKey((post.Tid, "EST")) ? DateTimeOffset.FromUnixTimeSeconds(ModDic[(post.Tid, "EST")]) : null,
                          RecommendExpirationDate = ModDic.ContainsKey((post.Tid, "EDI")) ? DateTimeOffset.FromUnixTimeSeconds(ModDic[(post.Tid, "EDI")]) : null,
                          HighlightExpirationDate = ModDic.ContainsKey((post.Tid, "EHL")) ? DateTimeOffset.FromUnixTimeSeconds(ModDic[(post.Tid, "EHL")]) : null,
                          CommentDisabledExpirationDate = ModDic.ContainsKey((post.Tid, "ECL")) ? DateTimeOffset.FromUnixTimeSeconds(ModDic[(post.Tid, "ECL")]) : null,
                          InVisibleArticleExpirationDate = ModDic.ContainsKey((post.Tid, "BNP")) ? DateTimeOffset.FromUnixTimeSeconds(ModDic[(post.Tid, "BNP")]) :
                                                           ModDic.ContainsKey((post.Tid, "UBN")) ? DateTimeOffset.FromUnixTimeSeconds(ModDic[(post.Tid, "UBN")]) : null,
                          Signature = post.Usesig,
                          Warning = post.Warning != null,
                          CreatorId = postResult.MemberId,
                          ModifierId = postResult.MemberId,
                          CreationDate = postResult.CreateDate,
                          ModificationDate = postResult.CreateDate
                      };

        sb.AppendCopyValues(article.Id, article.BoardId, article.CategoryId, (int) article.Status, (int) article.VisibleType,
                            (int) article.Type, (int) article.ContentType, (int) article.PinType, article.Title.ToCopyText(),
                            article.Content.ToCopyText(), article.ViewCount, article.ReplyCount, article.SortingIndex, article.LastReplyDate.ToCopyValue(),
                            article.LastReplierId.ToCopyValue(), article.PinPriority,
                            article.Cover.ToCopyValue(), article.Tag, article.RatingCount, article.ShareCount,
                            article.ImageCount, article.VideoCount, article.DonatePoint, article.Highlight, article.HighlightColor.ToCopyValue(),
                            article.Recommend, article.ReadPermission, article.CommentDisabled, (int) article.CommentVisibleType, article.LikeCount,
                            article.Ip, article.Price, article.AuditorId.ToCopyValue(), article.AuditFloor.ToCopyValue(),
                            article.SchedulePublishDate.ToCopyValue(), article.HideExpirationDate.ToCopyValue(), article.PinExpirationDate.ToCopyValue(),
                            article.RecommendExpirationDate.ToCopyValue(), article.HighlightExpirationDate.ToCopyValue(), article.CommentDisabledExpirationDate.ToCopyValue(),
                            article.InVisibleArticleExpirationDate.ToCopyValue(), article.Signature, article.Warning,
                            article.CreationDate, article.CreatorId, article.ModificationDate, article.ModifierId, article.Version);
    }

    private static ArticleCoverRelation? SetArticleCoverRelation(PostResult postResult, StringBuilder coverSb)
    {
        var post = postResult.Post;

        var isCover = post.Cover is not ("" or "0");
        var coverPath = isCover ? CoverHelper.GetCoverPath(post.Tid, post.Cover) : CoverHelper.GetThumbPath(post.Tid, post.Thumb);

        if (string.IsNullOrEmpty(coverPath)) return null;

        var coverRelation = new ArticleCoverRelation
                            {
                                Id = postResult.ArticleId,
                                OriginCover = isCover ? post.Cover : post.Thumb,
                                Tid = post.Tid,
                                Pid = Convert.ToInt32(post.Tid),
                                AttachmentUrl = isCover ? CoverHelper.GetCoverPath(post.Tid, post.Cover) : CoverHelper.GetThumbPath(post.Tid, post.Thumb)
                            };

        coverSb.AppendCopyValues(coverRelation.Id, coverRelation.OriginCover, coverRelation.Tid, coverRelation.Pid, coverRelation.AttachmentUrl);

        return coverRelation;
    }

    private static void SetArticleReward(PostResult postResult, StringBuilder rewardSb, IDictionary<int, ArticleReward> rewardDic)
    {
        var post = postResult.Post;

        if (post.Special != 3) return;

        var reward = new ArticleReward
                     {
                         Id = postResult.ArticleId,
                         Point = post.Price,
                         ExpirationDate = postResult.CreateDate.AddDays(CommonSetting.RewardExpirationDay),
                         SolveCommentId = null,
                         SolveDate = null,
                         AllowAdminSolveDate = postResult.CreateDate.AddDays(1),
                         CreationDate = postResult.CreateDate,
                         CreatorId = postResult.MemberId,
                         ModifierId = postResult.MemberId,
                         ModificationDate = postResult.CreateDate
                     };

        if (post.Price >= 0) //未解決
        {
            rewardSb.AppendCopyValues(reward.Id, reward.Point, reward.ExpirationDate,
                                      reward.SolveCommentId.ToCopyValue(), reward.SolveDate.ToCopyValue(), reward.AllowAdminSolveDate,
                                      reward.CreationDate, reward.CreatorId, reward.ModificationDate, reward.ModifierId, reward.Version);
        }
        else
        {
            rewardDic.Add(post.Tid, reward);
        }
    }

    private static void SetArticleRewardSolved(PostResult postResult, StringBuilder rewardSb, IDictionary<int, ArticleReward> rewardDic, long commentId)
    {
        var post = postResult.Post;

        if (!rewardDic.ContainsKey(post.Tid) || rewardDic[post.Tid].CreationDate.AddSeconds(1) != postResult.CreateDate) return;

        var reward = rewardDic[post.Tid];
        reward.Point = Math.Abs(reward.Point);
        reward.SolveDate = postResult.CreateDate;
        reward.SolveCommentId = commentId;

        rewardSb.AppendCopyValues(reward.Id, reward.Point, reward.ExpirationDate,
                                  reward.SolveCommentId.ToCopyValue(), reward.SolveDate.ToCopyValue(), reward.AllowAdminSolveDate,
                                  reward.CreationDate, reward.CreatorId, reward.ModificationDate, reward.ModifierId, reward.Version);

        rewardDic.Remove(post.Tid);
    }

    private static async Task<StringBuilder> SetCommentFirstAsync(PostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        var comment = new Comment
                      {
                          Id = postResult.ArticleId,
                          RootId = postResult.ArticleId,
                          Level = 1,
                          Hierarchy = postResult.ArticleId.ToString(),
                          Content = string.Empty,
                          VisibleType = VisibleType.Public,
                          Ip = postResult.Post.Useip,
                          Sequence = 1,
                          SortingIndex = postResult.CreateMilliseconds,
                          CreationDate = postResult.CreateDate,
                          CreatorId = postResult.MemberId,
                          ModificationDate = postResult.CreateDate,
                      };

        commentSb = await AppendCommentSbAsync(comment, commentSb, period, postTableId, cancellationToken);

        commentExtendDataSb.AppendCopyValues(postResult.ArticleId, EXTEND_DATA_BOARD_ID, postResult.BoardId,
                                             comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);

        return commentSb;
    }

    private async Task<StringBuilder> SetCommentAsync(PostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, Period period, int postTableId, long commentId, CancellationToken cancellationToken)
    {
        var post = postResult.Post;

        var comment = new Comment
                      {
                          Id = commentId,
                          RootId = postResult.ArticleId,
                          ParentId = postResult.ArticleId,
                          Level = 2,
                          Hierarchy = $"{postResult.ArticleId}/{commentId}",
                          Content = RegexHelper.GetNewMessage(post.Message, post.Tid),
                          VisibleType = post.Status == 1 ? VisibleType.Private : VisibleType.Public,
                          Ip = post.Useip,
                          Sequence = (int) post.Position - 1,
                          SortingIndex = postResult.CreateMilliseconds,
                          RelatedScore = post.Likescore,
                          ReplyCount = post.Replies,
                          LikeCount = 0,
                          DislikeCount = 0,
                          IsDeleted = post.Invisible,
                          CreationDate = postResult.CreateDate,
                          CreatorId = postResult.MemberId,
                          ModificationDate = postResult.CreateDate,
                      };

        commentSb = await AppendCommentSbAsync(comment, commentSb, period, postTableId, cancellationToken);

        if (post.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(post.StickDateline.Value);

            commentExtendDataSb.AppendCopyValues(commentId, EXTEND_DATA_RECOMMEND_COMMENT, true,
                                                 stickDate, 0, stickDate, 0, 0);
        }

        if (!post.Comment) return commentSb;

        var postComments = (await CommentHelper.GetPostCommentsAsync(post.Tid, post.Pid, cancellationToken)).ToArray();

        if (!postComments.Any())
            return commentSb;

        var sequence = 1;

        foreach (var postComment in postComments)
        {
            var commentReplyId = _snowflake.Generate();
            var replyDate = DateTimeOffset.FromUnixTimeSeconds(postComment.Dateline);

            var commentReply = new Comment
                               {
                                   Id = commentReplyId,
                                   RootId = postResult.ArticleId,
                                   ParentId = commentId,
                                   Level = 3,
                                   Hierarchy = $"{postResult.ArticleId}/{commentId}/{commentReplyId}",
                                   Content = RegexHelper.GetNewMessage(postComment.Comment, post.Tid),
                                   VisibleType = VisibleType.Public,
                                   Ip = postComment.Useip,
                                   Sequence = sequence++,
                                   SortingIndex = Convert.ToInt64(postComment.Dateline) * 1000,
                                   RelatedScore = 0,
                                   CreationDate = replyDate,
                                   CreatorId = postResult.SimpleMemberDic.ContainsKey(postComment.Authorid) ? postResult.SimpleMemberDic[postComment.Authorid].Item1 : 0,
                                   ModificationDate = replyDate,
                               };

           commentSb = await AppendCommentSbAsync(commentReply, commentSb, period, postTableId, cancellationToken);
        }

        return commentSb;
    }

    private static async Task<StringBuilder> AppendCommentSbAsync(Comment comment, StringBuilder commentSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        if (commentSb.Length > 30000)
        {
            await WriteToFileAsync($"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb, cancellationToken);

            commentSb = new StringBuilder();
        }

        commentSb.AppendCopyValues(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy, comment.SortingIndex,
                                   comment.Content.ToCopyText(), (int) comment.VisibleType, comment.Ip!, comment.Sequence,
                                   comment.RelatedScore, comment.ReplyCount, comment.LikeCount, comment.DislikeCount, comment.IsDeleted,
                                   comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);

        return commentSb;
    }

    private void SetArticleWarning(PostResult postResult, StringBuilder warningSb)
    {
        var post = postResult.Post;

        if (post.Warning == null) return;

        var warningDate = DateTimeOffset.FromUnixTimeSeconds(post.Warning.Dateline);

        var warning = new Warning
                      {
                          Id = _snowflake.Generate(),
                          WarningType = WarningType.Article,
                          SourceId = postResult.ArticleId,
                          MemberId = post.Warning.Authorid,
                          WarnerId = post.Warning.Operatorid,
                          Reason = post.Warning.Reason,
                          CreationDate = warningDate,
                          ModificationDate = warningDate,
                          CreatorId = post.Warning.Operatorid,
                      };

        warningSb.AppendCopyValues(warning.Id, (int) warning.WarningType, warning.SourceId, warning.MemberId, warning.WarnerId, warning.Reason,
                                   warning.CreationDate, warning.CreatorId, warning.ModificationDate, warning.ModifierId, warning.Version);
    }

    private static async Task WriteToFileAsync(string directoryPath, string fileName, string copyPrefix, StringBuilder valueSb, CancellationToken cancellationToken)
    {
        if (valueSb.Length == 0)
            return;

        Directory.CreateDirectory(directoryPath);
        var fullPath = $"{directoryPath}/{fileName}";

        if (File.Exists(fullPath))
        {
            await File.AppendAllTextAsync(fullPath, valueSb.ToString(), cancellationToken);
            Console.WriteLine($"Append {fullPath}");
        }
        else
        {
            await File.WriteAllTextAsync(fullPath, string.Concat(copyPrefix, valueSb.ToString()), cancellationToken);
            Console.WriteLine(fullPath);
        }
    }
}