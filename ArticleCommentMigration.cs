using System.Diagnostics;
using System.Text;
using Dapper;
using ForumDataMigration.Enums;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Domain.Entities;
using Lctech.Jkf.Forum.Enums;
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
        var jsonSb = new StringBuilder();
        var coverSb = new StringBuilder();
        var commentSb = new StringBuilder();
        var commentJsonSb = new StringBuilder();
        var commentExtendDataSb = new StringBuilder();

        //var warningSb = new StringBuilder();
        var rewardSb = new StringBuilder();

        var dic = await RelationHelper.GetArticleIdDicAsync(posts.Select(x => x.Tid).Distinct().ToArray(), cancellationToken);
        var simpleMemberDic = await RelationHelper.GetSimpleMemberDicAsync(posts.Select(x => x.Authorid).Distinct().ToArray(), cancellationToken);
        var lastPosterNames = posts.Select(x => x.Lastposter).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();

        var memberDisplayNameDic = new Dictionary<string, long>();

        if (lastPosterNames.Any())
            memberDisplayNameDic = await RelationHelper.GetMembersDisplayNameDicAsync(lastPosterNames!, cancellationToken);

        var rewardDic = new Dictionary<int, ArticleReward>(); //有解決的懸賞文章要暫存

        var sw = new Stopwatch();
        sw.Start();

        var attachPathDic = await RegexHelper.GetAttachFileNameDicAsync(posts, cancellationToken);

        sw.Stop();
        Console.WriteLine($"selectMany Time => {sw.ElapsedMilliseconds}ms");

        foreach (var post in posts)
        {
            var id = dic.GetValueOrDefault(post.Tid);
            var boardId = BoardDic.GetValueOrDefault(post.Fid);

            //髒資料放過他
            if (id == 0 || boardId == 0)
                continue;

            var (memberId, memberName) = simpleMemberDic.GetValueOrDefault(post.Authorid);

            if (memberId == 0)
                continue;

            var postResult = new PostResult
                             {
                                 ArticleId = id,
                                 BoardId = boardId,
                                 MemberId = memberId,
                                 MemberName = memberName,
                                 LastPosterId = !string.IsNullOrEmpty(post.Lastposter) && memberDisplayNameDic.ContainsKey(post.Lastposter) ? memberDisplayNameDic[post.Lastposter] : null,
                                 CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline),
                                 CreateMilliseconds = Convert.ToInt64(post.Dateline) * 1000,
                                 Post = post,
                                 AttachPathDic = attachPathDic
                             };

            if (post.First) //文章
            {
                await CommonHelper.WatchTimeAsync("SetArticleAsync", async () => await SetArticleAsync(postResult, sb, coverSb, jsonSb, cancellationToken));
                CommonHelper.WatchTime("SetArticleReward", () => SetArticleReward(postResult, rewardSb, rewardDic));

                //SetArticleWarning(postResult,warningSb);
                await CommonHelper.WatchTimeAsync("SetCommentFirstAsync", async () => await SetCommentFirstAsync(postResult, commentSb, commentExtendDataSb, commentJsonSb, period, postTableId, cancellationToken));
            }
            else if (post.Position != 1) //留言
            {
                var commentId = _snowflake.Generate();
                CommonHelper.WatchTime("SetArticleRewardSolved", () => SetArticleRewardSolved(postResult, rewardSb, rewardDic, commentId));
                await CommonHelper.WatchTimeAsync("SetCommentAsync", async () => await SetCommentAsync(postResult, commentSb, commentExtendDataSb, commentJsonSb, period, postTableId, commentId, cancellationToken));
            }
        }

        var task = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(Article)}/{period.FolderName}", $"{postTableId}.sql", COPY_PREFIX, sb); });
        var jsonTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{ARTICLE_JSON}/{period.FolderName}", $"{postTableId}.sql", "", jsonSb); });
        var coverTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}/{period.FolderName}", $"{postTableId}.sql", COVER_RELATION_PREFIX, coverSb); });
        var rewardTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}/{period.FolderName}", $"{postTableId}.sql", COPY_REWARD_PREFIX, rewardSb); });

        // var warningTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(Warning)}/{period.FolderName}", $"{postTableId}.sql", COPY_WARNING_PREFIX, warningSb); });
        var commentTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb); });
        var commentExtendDataTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(CommentExtendData)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_EXTEND_DATA_PREFIX, commentExtendDataSb); });
        var commentJsonTask = new Task(() => { WriteToFile($"{Setting.INSERT_DATA_PATH}/{COMMENT_JSON}/{period.FolderName}", $"{postTableId}.sql", "", commentJsonSb); });

        task.Start();
        jsonTask.Start();
        coverTask.Start();
        rewardTask.Start();
        commentTask.Start();
        commentExtendDataTask.Start();
        commentJsonTask.Start();

        await Task.WhenAll(task, jsonTask, coverTask, rewardTask, commentTask, commentExtendDataTask, commentJsonTask);
    }

    private static async Task SetArticleAsync(PostResult postResult, StringBuilder sb, StringBuilder coverSb, StringBuilder jsonSb, CancellationToken cancellationToken)
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
                          Status = isScheduling ? ArticleStatus.Scheduling :
                                       ArticleStatus.Published,
                          DeleteStatus = post.Displayorder == -1 ? DeleteStatus.Deleted : DeleteStatus.None,
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
                          Content = RegexHelper.GetNewMessage(post.Message, post.Tid, postResult.AttachPathDic),
                          ViewCount = post.Views,
                          ReplyCount = post.Replies,
                          BoardId = postResult.BoardId,
                          CategoryId = CategoryDic.GetValueOrDefault(post.Typeid),
                          SortingIndex = postResult.CreateMilliseconds,
                          LastReplyDate = post.Lastpost.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Lastpost.Value) : null,
                          LastReplierId = postResult.LastPosterId,
                          PinPriority = post.Displayorder,
                          Cover = SetArticleCoverRelation(postResult, coverSb)?.Id,
                          Tag = post.Tags.ToNewTags(),
                          RatingCount = post.Ratetimes ?? 0,
                          ShareCount = post.Sharetimes,
                          ImageCount = imageCount,
                          VideoCount = videoCount,
                          DonatePoint = 0,
                          Highlight = post.Highlight != 0,
                          HighlightColor = ColorDic.GetValueOrDefault(highlightInt),
                          Recommend = post.Digest,
                          ReadPermission = post.Readperm,
                          CommentDisabled = post.Closed == 1,
                          CommentVisibleType = post.Status == 34 ? VisibleType.Private : VisibleType.Public,
                          LikeCount = post.ThankCount ?? 0,
                          Ip = post.Useip,
                          Price = post.Price,
                          AuditorId = read?.ReadUid,
                          AuditFloor = read?.ReadFloor,
                          PublishDate = post.PostTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.PostTime.Value) : postResult.CreateDate,
                          HideExpirationDate = BbCodeHideTagRegex.IsMatch(post.Message) ? postResult.CreateDate.AddDays(CommonSetting.HideExpirationDay) : null,
                          PinExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EST")).ToDatetimeOffset(),
                          RecommendExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EDI")).ToDatetimeOffset(),
                          HighlightExpirationDate = ModDic.GetValueOrDefault((post.Tid, "EHL")).ToDatetimeOffset(),
                          CommentDisabledExpirationDate = ModDic.GetValueOrDefault((post.Tid, "ECL")).ToDatetimeOffset(),
                          InVisibleArticleExpirationDate = ModDic.GetValueOrDefault((post.Tid, "BNP")).ToDatetimeOffset() ??
                                                           ModDic.GetValueOrDefault((post.Tid, "UBN")).ToDatetimeOffset(),
                          Signature = post.Usesig,
                          CreatorId = postResult.MemberId,
                          ModifierId = postResult.MemberId,
                          CreationDate = postResult.CreateDate,
                          ModificationDate = postResult.CreateDate
                      };

        sb.AppendValueLine(article.Id, article.BoardId, article.CategoryId.ToCopyValue(), (int) article.Status, (int) article.DeleteStatus,
                           (int) article.VisibleType, (int) article.Type, (int) article.ContentType, (int) article.PinType, article.Title.ToCopyText(),
                           article.Content.ToCopyText(), article.ViewCount, article.ReplyCount, article.SortingIndex, article.LastReplyDate.ToCopyValue(),
                           article.LastReplierId.ToCopyValue(), article.PinPriority,
                           article.Cover.ToCopyValue(), article.Tag, article.RatingCount, article.ShareCount,
                           article.ImageCount, article.VideoCount, article.DonatePoint, article.Highlight, article.HighlightColor.ToCopyValue(),
                           article.Recommend, article.ReadPermission, article.CommentDisabled, (int) article.CommentVisibleType, article.LikeCount,
                           article.Ip, article.Price, article.AuditorId.ToCopyValue(), article.AuditFloor.ToCopyValue(),
                           article.PublishDate, article.HideExpirationDate.ToCopyValue(), article.PinExpirationDate.ToCopyValue(),
                           article.RecommendExpirationDate.ToCopyValue(), article.HighlightExpirationDate.ToCopyValue(), article.CommentDisabledExpirationDate.ToCopyValue(),
                           article.InVisibleArticleExpirationDate.ToCopyValue(), article.Signature,
                           article.CreationDate, article.CreatorId, article.ModificationDate, article.ModifierId, article.Version);


        #region Es文件檔

        jsonSb.Append(ArticleEsIdPrefix).Append(article.Id).AppendLine(EsIdSuffix);

        var document = SetArticleDocument(article, postResult);

        var json = await JsonHelper.GetJsonAsync(document, cancellationToken);

        jsonSb.AppendLine(json);

        #endregion
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

        coverSb.AppendValueLine(coverRelation.Id, coverRelation.OriginCover, coverRelation.Tid, coverRelation.Pid, coverRelation.AttachmentUrl);

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
                         ModificationDate = postResult.CreateDate,
                         ModifierId = postResult.MemberId
                     };

        if (post.Price >= 0) //未解決
        {
            rewardSb.AppendValueLine(reward.Id, reward.Point, reward.ExpirationDate,
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

        rewardSb.AppendValueLine(reward.Id, reward.Point, reward.ExpirationDate,
                                 reward.SolveCommentId.ToCopyValue(), reward.SolveDate.ToCopyValue(), reward.AllowAdminSolveDate,
                                 reward.CreationDate, reward.CreatorId, reward.ModificationDate, reward.ModifierId, reward.Version);

        rewardDic.Remove(post.Tid);
    }

    private static async Task SetCommentFirstAsync(PostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder commentJsonSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        var comment = new Comment
                      {
                          Id = postResult.ArticleId,
                          RootId = postResult.ArticleId,
                          Level = 1,
                          Hierarchy = postResult.ArticleId.ToString(),
                          Title = postResult.Post.Subject,
                          Content = string.Empty,
                          VisibleType = VisibleType.Public,
                          Ip = postResult.Post.Useip,
                          Sequence = 1,
                          SortingIndex = postResult.CreateMilliseconds,
                          CreationDate = postResult.CreateDate,
                          CreatorId = postResult.MemberId,
                          ModificationDate = postResult.CreateDate,
                          ModifierId = postResult.MemberId
                      };

        await AppendCommentSbAsync(postResult, comment, commentSb, commentJsonSb, period, postTableId, cancellationToken);

        commentExtendDataSb.AppendValueLine(postResult.ArticleId, EXTEND_DATA_BOARD_ID, postResult.BoardId,
                                            comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }

    private async Task SetCommentAsync(PostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder commentJsonSb, Period period, int postTableId, long commentId, CancellationToken cancellationToken)
    {
        var post = postResult.Post;

        var comment = new Comment
                      {
                          Id = commentId,
                          RootId = postResult.ArticleId,
                          ParentId = postResult.ArticleId,
                          Level = 2,
                          Hierarchy = $"{postResult.ArticleId}/{commentId}",
                          Content = RegexHelper.GetNewMessage(post.Message, post.Tid, postResult.AttachPathDic),
                          VisibleType = post.Status == 1 ? VisibleType.Hidden : VisibleType.Public,
                          Ip = post.Useip,
                          Sequence = (int) post.Position - 1,
                          SortingIndex = postResult.CreateMilliseconds,
                          RelatedScore = post.Likescore,
                          ReplyCount = post.Replies,
                          LikeCount = 0,
                          DislikeCount = 0,
                          DeleteStatus = post.Invisible ? DeleteStatus.Deleted : DeleteStatus.None,
                          CreationDate = postResult.CreateDate,
                          CreatorId = postResult.MemberId,
                          ModificationDate = postResult.CreateDate,
                          ModifierId = postResult.MemberId,
                      };

        await AppendCommentSbAsync(postResult, comment, commentSb, commentJsonSb, period, postTableId, cancellationToken);

        if (post.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(post.StickDateline.Value);

            commentExtendDataSb.AppendValueLine(commentId, EXTEND_DATA_RECOMMEND_COMMENT, true,
                                                stickDate, 0, stickDate, 0, 0);
        }

        if (!post.Comment) return;

        var postComments = (await CommentHelper.GetPostCommentsAsync(post.Tid, post.Pid, cancellationToken)).ToArray();

        if (!postComments.Any())
            return;

        var authorIds = postComments.Select(x => x.Authorid).Distinct().ToArray();
        var membersUidDic = await RelationHelper.GetMembersUidDicAsync(authorIds, cancellationToken);

        var sequence = 1;

        foreach (var postComment in postComments)
        {
            var commentReplyId = _snowflake.Generate();
            var replyDate = DateTimeOffset.FromUnixTimeSeconds(postComment.Dateline);
            var memberId = membersUidDic.ContainsKey(postComment.Authorid) ? membersUidDic[postComment.Authorid] : 0;

            postResult.ReplyMemberUid = postComment.Authorid;
            postResult.ReplyMemberName = postComment.Author ?? string.Empty;

            var commentReply = new Comment
                               {
                                   Id = commentReplyId,
                                   RootId = postResult.ArticleId,
                                   ParentId = commentId,
                                   Level = 3,
                                   Hierarchy = $"{postResult.ArticleId}/{commentId}/{commentReplyId}",
                                   Content = RegexHelper.GetNewMessage(postComment.Comment, post.Tid, postResult.AttachPathDic),
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

    private static async Task AppendCommentSbAsync(PostResult postResult, Comment comment, StringBuilder commentSb, StringBuilder commentJsonSb, Period period, int postTableId, CancellationToken cancellationToken)
    {
        const int maxStringBuilderLength = 60000;

        if (commentSb.Length > maxStringBuilderLength)
        {
            WriteToFile($"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}", $"{postTableId}.sql", COPY_COMMENT_PREFIX, commentSb);

            commentSb.Clear();
        }

        commentSb.AppendValueLine(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy, comment.SortingIndex,
                                  comment.Title!.ToCopyText(), comment.Content.ToCopyText(), (int) comment.VisibleType, comment.Ip!, comment.Sequence,
                                  comment.RelatedScore, comment.ReplyCount, comment.LikeCount, comment.DislikeCount, (int) comment.DeleteStatus,
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

    // private void SetArticleWarning(PostResult postResult, StringBuilder warningSb)
    // {
    //     var post = postResult.Post;
    //
    //     if (post.Warning == null) return;
    //
    //     var warningDate = DateTimeOffset.FromUnixTimeSeconds(post.Warning.Dateline);
    //
    //     var warning = new Warning
    //                   {
    //                       Id = _snowflake.Generate(),
    //                       WarningType = WarningType.Article,
    //                       SourceId = postResult.ArticleId,
    //                       MemberId = post.Warning.Authorid,
    //                       WarnerId = post.Warning.Operatorid,
    //                       Reason = post.Warning.Reason,
    //                       CreationDate = warningDate,
    //                       ModificationDate = warningDate,
    //                       CreatorId = post.Warning.Operatorid,
    //                   };
    //
    //     warningSb.AppendCopyValues(warning.Id, (int) warning.WarningType, warning.SourceId, warning.MemberId, warning.WarnerId, warning.Reason,
    //                                warning.CreationDate, warning.CreatorId, warning.ModificationDate, warning.ModifierId, warning.Version);
    // }

    private static Document SetArticleDocument(Article article, PostResult postResult)
    {
        return new Document()
               {
                   //article part
                   Id = article.Id,
                   Title = RegexHelper.CleanText(article.Title) ?? string.Empty,
                   Content = RegexHelper.CleanText(article.Content) ?? string.Empty,
                   ReadPermission = article.ReadPermission,
                   Tag = article.Tag,
                   ThumbnailId = article.Cover,
                   BoardId = article.BoardId,
                   CategoryId = article.CategoryId,
                   Sequence = 1,
                   SortingIndex = article.SortingIndex,
                   Score = 0,
                   Ip = article.Ip,
                   PinType = article.PinType,
                   PinPriority = 0,
                   VisibleType = article.VisibleType,
                   Status = article.Status,
                   LastReplyDate = article.LastReplyDate,
                   LastReplierId = article.LastReplierId,
                   CreationDate = article.CreationDate,
                   CreatorId = article.CreatorId,
                   ModificationDate = article.ModificationDate,
                   ModifierId = article.ModifierId,

                   //document part
                   Type = DocumentType.Thread,
                   DeleteStatus = article.DeleteStatus,
                   CreatorUid = postResult.Post.Authorid,
                   CreatorName = postResult.MemberName,
                   ModifierUid = postResult.Post.Authorid,
                   ModifierName = postResult.MemberName,
                   Relationship = new Relationship()
                                  {
                                      Name = ArticleRelationShipName
                                  }
               };
    }

    private static Document SetCommentDocument(Comment comment, PostResult postResult)
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
                   VisibleType = comment.VisibleType,
                   CreationDate = comment.CreationDate,
                   CreatorId = comment.CreatorId,
                   ModificationDate = comment.ModificationDate,
                   ModifierId = comment.ModifierId,

                   //document part
                   Type = DocumentType.Comment,
                   DeleteStatus = comment.DeleteStatus,
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