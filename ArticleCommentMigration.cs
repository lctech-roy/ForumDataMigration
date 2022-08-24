using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Lctech.Jkf.Domain.Enums;
using MySqlConnector;
using Netcorext.Algorithms;

namespace ForumDataMigration;

public class ArticleCommentMigration
{
    private readonly ISnowflake _snowflake;
    private const string IMAGE_PATTERN = @"\[(?:img|attachimg)](.*?)\[\/(?:img|attachimg)]";
    private const string VIDEO_PATTERN = @"\[(media[^\]]*|video)](.*?)\[\/(media|video)]";
    private const string HIDE_PATTERN = @"(\[\/?hide[^\]]*\]|{[^}]*})";

    private static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeHideTagRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Dictionary<int, string?> ColorDic = new()
                                                                {
                                                                    { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                                                                };

    public ArticleCommentMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        const string articleSql = $"COPY \"{nameof(Article)}\" " +
                                  $"(\"{nameof(Article.Id)}\",\"{nameof(Article.BoardId)}\",\"{nameof(Article.CategoryId)}\",\"{nameof(Article.Status)}\"" +
                                  $",\"{nameof(Article.VisibleType)}\",\"{nameof(Article.Type)}\",\"{nameof(Article.ContentType)}\",\"{nameof(Article.PinType)}\"" +
                                  $",\"{nameof(Article.Title)}\",\"{nameof(Article.Content)}\",\"{nameof(Article.ViewCount)}\",\"{nameof(Article.ReplyCount)}\"" +
                                  $",\"{nameof(Article.SortingIndex)}\",\"{nameof(Article.LastReplyDate)}\",\"{nameof(Article.LastReplierId)}\",\"{nameof(Article.PinPriority)}\"" +
                                  $",\"{nameof(Article.Cover)}\",\"{nameof(Article.Tag)}\",\"{nameof(Article.RatingCount)}\"" +
                                  $",\"{nameof(Article.ShareCount)}\",\"{nameof(Article.ImageCount)}\",\"{nameof(Article.VideoCount)}\",\"{nameof(Article.DonatePoint)}\"" +
                                  $",\"{nameof(Article.Highlight)}\",\"{nameof(Article.HighlightColor)}\",\"{nameof(Article.Recommend)}\",\"{nameof(Article.ReadPermission)}\"" +
                                  $",\"{nameof(Article.CommentDisabled)}\",\"{nameof(Article.CommentVisibleType)}\",\"{nameof(Article.LikeCount)}\",\"{nameof(Article.Ip)}\"" +
                                  $",\"{nameof(Article.Price)}\",\"{nameof(Article.AuditorId)}\",\"{nameof(Article.AuditFloor)}\",\"{nameof(Article.SchedulePublishDate)}\"" +
                                  $",\"{nameof(Article.HideExpirationDate)}\",\"{nameof(Article.PinExpirationDate)}\",\"{nameof(Article.RecommendExpirationDate)}\",\"{nameof(Article.HighlightExpirationDate)}\"" +
                                  $",\"{nameof(Article.CommentDisabledExpirationDate)}\",\"{nameof(Article.InVisibleArticleExpirationDate)}\",\"{nameof(Article.Signature)}\",\"{nameof(Article.Warning)}\"" +
                                  Setting.COPY_ENTITY_SUFFIX;

        const string articleCoverRelationSql = $"COPY \"{nameof(ArticleCoverRelation)}\" " +
                                               $"(\"{nameof(ArticleCoverRelation.Id)}\",\"{nameof(ArticleCoverRelation.OriginCover)}\",\"{nameof(ArticleCoverRelation.Tid)}\",\"{nameof(ArticleCoverRelation.Pid)}\",\"{nameof(ArticleCoverRelation.AttachmentUrl)}\"" + Setting.COPY_SUFFIX;

        const string articleRewardSql = $"COPY \"{nameof(ArticleReward)}\" " +
                                        $"(\"{nameof(ArticleReward.Id)}\",\"{nameof(ArticleReward.Point)}\",\"{nameof(ArticleReward.ExpirationDate)}\"" +
                                        $",\"{nameof(ArticleReward.SolveCommentId)}\",\"{nameof(ArticleReward.SolveDate)}\",\"{nameof(ArticleReward.AllowAdminSolveDate)}\"" + Setting.COPY_ENTITY_SUFFIX;

        const string warningSql = $"COPY \"{nameof(Warning)}\" " +
                                  $"(\"{nameof(Warning.Id)}\",\"{nameof(Warning.WarningType)}\",\"{nameof(Warning.SourceId)}\",\"{nameof(Warning.MemberId)}\"" +
                                  $",\"{nameof(Warning.WarnerId)}\",\"{nameof(Warning.Reason)}\"" + Setting.COPY_ENTITY_SUFFIX;

        const string commentSql = $"COPY \"{nameof(Comment)}\" " +
                                  $"(\"{nameof(Comment.Id)}\",\"{nameof(Comment.RootId)}\",\"{nameof(Comment.ParentId)}\",\"{nameof(Comment.Level)}\",\"{nameof(Comment.Hierarchy)}\"" +
                                  $",\"{nameof(Comment.SortingIndex)}\",\"{nameof(Comment.Content)}\",\"{nameof(Comment.VisibleType)}\",\"{nameof(Comment.Ip)}\"" +
                                  $",\"{nameof(Comment.Sequence)}\",\"{nameof(Comment.RelatedScore)}\",\"{nameof(Comment.ReplyCount)}\",\"{nameof(Comment.LikeCount)}\"" +
                                  $",\"{nameof(Comment.DislikeCount)}\",\"{nameof(Comment.IsDeleted)}\"" + Setting.COPY_ENTITY_SUFFIX;

        const string commentExtendDataSql = $"COPY \"{Setting.COMMENT_EXTEND_DATA}\" (\"Id\",\"Key\",\"Value\"" + Setting.COPY_ENTITY_SUFFIX;


        #region 轉檔前準備相關資料

        var dic = RelationContainer.ArticleIdDic;
        var boardDic = RelationHelper.GetBoardDic();
        var categoryDic = RelationHelper.GetCategoryDic();
        var memberUidDic = RelationHelper.GetMemberUidDic();
        var memberDisplayNameDic = RelationHelper.GetMemberDisplayNameDic();
        var modDic = ArticleHelper.GetModDic();
        var readDic = ArticleHelper.GetReadDic();
        var periods = PeriodHelper.GetPeriods();
        var postTableIds = ArticleHelper.GetPostTableIds(150);
        var commonSetting = ArticleHelper.GetCommonSetting();

        #endregion

        foreach (var period in periods)
        {
            Parallel.ForEach(postTableIds, //new ParallelOptions { MaxDegreeOfParallelism = 1 },
                             postTableId =>
                             {
                                 var sql = $@"SELECT 
                                    thread.displayorder , thread.special , thread.subject ,  
                                    thread.closed, thread.views , thread.replies,  
                                    thread.lastpost,thread.lastposter, thread.sharetimes,  
                                    thread.typeid, thread.highlight, thread.price,
                                    thread.digest, thread.readperm,thread.cover,thread.thumb,
                                    post.tid AS Tid,
                                    post.pid AS Pid,
                                    post.fid AS Fid,
                                    post.authorid,
                                    post.dateline,
                                    post.message,
                                    post.ratetimes,
                                    post.useip,
                                    post.usesig,
                                    post.position,
                                    post.tags,
                                    post.status,
                                    post.first,
                                    post.comment,
                                    post.likescore,
                                    post.invisible,
                                    thankCount.count AS thankCount,
                                    postDelay.post_time AS postTime,
                                    postStick.dateline AS stickDateline
                                    -- warning.pid AS {nameof(ThreadWarning)}_Pid,
                                    -- warning.authorid AS {nameof(ThreadWarning)}_authorid,
                                    -- warning.operatorid AS {nameof(ThreadWarning)}_operatorid,
                                    -- warning.reason AS {nameof(ThreadWarning)}_reason,
                                    -- warning.dateline AS {nameof(ThreadWarning)}_dateline
                                    FROM (
                                      SELECT tid,pid,fid,message,ratetimes,useip,usesig,position,tags,status,first,comment,likescore,invisible,authorid,dateline
                                      -- FROM pre_forum_post
                                      FROM `pre_forum_post{(postTableId != 0 ? $"_{postTableId}" : "")}`
                                      WHERE dateline >= @Start AND dateline < @End --  AND position = 1 AND first = true
                                      -- FROM pre_forum_post_96 where tid = 11229114
                                    ) AS post
                                    LEFT JOIN pre_forum_thread AS thread ON thread.tid = post.tid AND post.first = true
                                    LEFT JOIN pre_forum_thankcount AS thankCount ON thankCount.tid = post.tid  AND post.first = true
                                    LEFT JOIN pre_post_delay AS postDelay ON postDelay.tid = thread.tid AND post.first = true
                                    LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
                                    -- LEFT JOIN pre_forum_warning as warning on warning.pid = post.pid
                                    ";

                                 {
                                     List<Post> posts;

                                     using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                     {
                                         posts = cn.Query<Post>(sql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToList();

                                         //posts = Slapper.AutoMapper.MapDynamic<Post>(dynamicPosts, false).ToList();
                                     }

                                     if (!posts.Any())
                                         return;

                                     var sb = new StringBuilder();
                                     var coverSb = new StringBuilder();
                                     var commentSb = new StringBuilder();
                                     var commentExtendDataSb = new StringBuilder();
                                     var commentReplySb = new StringBuilder();
                                     var warningSb = new StringBuilder();
                                     var rewardSb = new StringBuilder();

                                     var rewardDic = new Dictionary<int, ArticleReward>(); //有解決的懸賞文章要暫存

                                     foreach (var post in posts)
                                     {
                                         //髒資料放過他
                                         if (!dic.ContainsKey(post.Tid) || !boardDic.ContainsKey(post.Fid))
                                             continue;

                                         var postResult = new PostResult
                                                          {
                                                              ArticleId = dic[post.Tid],
                                                              BoardId = boardDic[post.Fid],
                                                              MemberId = memberUidDic.ContainsKey(Convert.ToInt32(post.Authorid)) ? memberUidDic[Convert.ToInt32(post.Authorid)] : 0,
                                                              CreateDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline),
                                                              CreateMilliseconds = Convert.ToInt64(post.Dateline) * 1000,
                                                              HideExpirationDay = commonSetting.HideExpirationDay,
                                                              RewardExpirationDay = commonSetting.RewardExpirationDay,
                                                              MemberDisplayNameDic = memberDisplayNameDic,
                                                              MemberUidDic = memberUidDic,
                                                              ModDic = modDic,
                                                              CategoryDic = categoryDic,
                                                              ReadDic = readDic,
                                                              Post = post
                                                          };

                                         if (post.First) //文章
                                         {
                                             SetArticle(postResult, sb, coverSb);
                                             SetArticleReward(postResult, rewardSb, rewardDic);

                                             //SetArticleWarning(postResult,warningSb);
                                             SetCommentFirst(postResult, commentSb, commentExtendDataSb, period, postTableId, commentSql);
                                         }
                                         else if (post.Position != 1) //留言
                                         {
                                             var commentId = _snowflake.Generate();
                                             SetArticleRewardSolved(postResult, rewardSb, rewardDic, commentId);
                                             SetComment(postResult, commentSb, commentExtendDataSb, commentReplySb, period, postTableId, commentSql, commentId);
                                         }
                                     }

                                     if (sb.Length > 0)
                                     {
                                         var path = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}/{period.FolderName}";
                                         Directory.CreateDirectory(path);
                                         var fullPath = $"{path}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(articleSql, sb.ToString()));

                                         Console.WriteLine(fullPath);
                                     }

                                     if (coverSb.Length > 0)
                                     {
                                         var coverPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}/{period.FolderName}";
                                         Directory.CreateDirectory(coverPath);
                                         var fullPath = $"{coverPath}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(articleCoverRelationSql, coverSb.ToString()));
                                         Console.WriteLine(fullPath);
                                     }

                                     if (rewardSb.Length > 0)
                                     {
                                         var rewardPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}/{period.FolderName}";
                                         Directory.CreateDirectory(rewardPath);
                                         var fullPath = $"{rewardPath}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(articleRewardSql, rewardSb.ToString()));
                                         Console.WriteLine(fullPath);
                                     }

                                     if (warningSb.Length > 0)
                                     {
                                         var warningPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Warning)}/{period.FolderName}";
                                         Directory.CreateDirectory(warningPath);
                                         var fullPath = $"{warningPath}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(warningSql, warningSb.ToString()));

                                         Console.WriteLine(fullPath);
                                     }

                                     if (commentSb.Length > 0)
                                     {
                                         var commentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}";
                                         Directory.CreateDirectory(commentPath);
                                         var fullPath = $"{commentPath}/{postTableId}.sql";

                                         if (File.Exists(fullPath) && File.ReadLines(fullPath).Any())
                                             File.AppendAllText(fullPath, commentSb.ToString());
                                         else
                                             File.WriteAllText(fullPath, string.Concat(commentSql, commentSb.ToString()));

                                         if (commentReplySb.Length > 0)
                                             File.AppendAllText(fullPath, commentReplySb.ToString());

                                         Console.WriteLine(fullPath);
                                     }

                                     if (commentExtendDataSb.Length > 0)
                                     {
                                         var commentExtendDataPath = $"{Setting.INSERT_DATA_PATH}/{Setting.COMMENT_EXTEND_DATA}/{period.FolderName}";
                                         Directory.CreateDirectory(commentExtendDataPath);
                                         var fullPath = $"{commentExtendDataPath}/{postTableId}.sql";

                                         File.WriteAllText(fullPath, string.Concat(commentExtendDataSql, commentExtendDataSb.ToString()));
                                         Console.WriteLine(fullPath);
                                     }
                                 }
                             });
        }
    }

    private static void SetArticle(PostResult postResult, StringBuilder sb, StringBuilder coverSb)
    {
        var post = postResult.Post;

        var isScheduling = post.PostTime < post.Dateline;
        var highlightInt = post.Highlight % 10; //只要取個位數
        var read = postResult.ReadDic.ContainsKey(post.Tid) ? postResult.ReadDic[post.Tid] : null;
        var imageCount = BbCodeImageRegex.Matches(post.Message).Count;
        var videoCount = BbCodeVideoRegex.Matches(post.Message).Count;
        var modDic = postResult.ModDic;

        var article = new Article()
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
                          CategoryId = postResult.CategoryDic.ContainsKey(post.Typeid) ? postResult.CategoryDic[post.Typeid] : 0,
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
                          HideExpirationDate = BbCodeHideTagRegex.IsMatch(post.Message) ? postResult.CreateDate.AddDays(postResult.HideExpirationDay) : null,
                          PinExpirationDate = modDic.ContainsKey((post.Tid, "EST")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "EST")]) : null,
                          RecommendExpirationDate = modDic.ContainsKey((post.Tid, "EDI")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "EDI")]) : null,
                          HighlightExpirationDate = modDic.ContainsKey((post.Tid, "EHL")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "EHL")]) : null,
                          CommentDisabledExpirationDate = modDic.ContainsKey((post.Tid, "ECL")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "ECL")]) : null,
                          InVisibleArticleExpirationDate = modDic.ContainsKey((post.Tid, "BNP")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "BNP")]) :
                                                           modDic.ContainsKey((post.Tid, "UBN")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "UBN")]) : null,
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

        coverSb.AppendCopyValues(coverRelation.Id,coverRelation.OriginCover,coverRelation.Tid, coverRelation.Pid, coverRelation.AttachmentUrl);

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
                         ExpirationDate = postResult.CreateDate.AddDays(postResult.RewardExpirationDay),
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

    private static void SetCommentFirst(PostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, Period period, int postTableId, string commentSql)
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

        AppendCommentSb(comment, ref commentSb, period, postTableId, commentSql);

        commentExtendDataSb.AppendCopyValues(postResult.ArticleId, Setting.EXTEND_DATA_BOARD_ID, postResult.BoardId,
                                             comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }

    private void SetComment(PostResult postResult, StringBuilder commentSb, StringBuilder commentExtendDataSb, StringBuilder commentReplySb, Period period, int postTableId, string commentSql, long commentId)
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

        AppendCommentSb(comment, ref commentSb, period, postTableId, commentSql);

        if (post.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(post.StickDateline.Value);

            commentExtendDataSb.AppendCopyValues(commentId, Setting.EXTEND_DATA_RECOMMEND_COMMENT, true,
                                                 stickDate, 0, stickDate, 0, 0);
        }

        if (!post.Comment) return;

        var postComments = CommentHelper.GetPostComments(post.Tid, post.Pid);
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
                                   CreatorId = postResult.MemberUidDic.ContainsKey(postComment.Authorid) ? postResult.MemberUidDic[postComment.Authorid] : 0,
                                   ModificationDate = replyDate,
                               };

            commentReplySb.AppendCopyValues(commentReply.Id, commentReply.RootId, commentReply.ParentId, commentReply.Level, commentReply.Hierarchy, commentReply.SortingIndex,
                                            commentReply.Content.ToCopyText(), (int) commentReply.VisibleType, commentReply.Ip, commentReply.Sequence,
                                            commentReply.RelatedScore, commentReply.ReplyCount, commentReply.LikeCount, commentReply.DislikeCount, commentReply.IsDeleted,
                                            commentReply.CreationDate, commentReply.CreatorId, commentReply.ModificationDate, commentReply.ModifierId, commentReply.Version);
        }
    }

    private static void AppendCommentSb(Comment comment, ref StringBuilder commentSb, Period period, int postTableId, string commentSql)
    {
        if (commentSb.Length > 30000)
        {
            var path = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{period.FolderName}";
            var fullPath = $"{path}/{postTableId}.sql";

            if (File.Exists(fullPath) && File.ReadLines(fullPath).Any())
                File.AppendAllText(fullPath, commentSb.ToString());
            else
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(fullPath, string.Concat(commentSql, commentSb.ToString()));
            }

            commentSb = new StringBuilder();
        }

        commentSb.AppendCopyValues(comment.Id, comment.RootId, comment.ParentId.ToCopyValue(), comment.Level, comment.Hierarchy, comment.SortingIndex,
                                   comment.Content.ToCopyText(), (int) comment.VisibleType, comment.Ip!, comment.Sequence,
                                   comment.RelatedScore, comment.ReplyCount, comment.LikeCount, comment.DislikeCount, comment.IsDeleted,
                                   comment.CreationDate, comment.CreatorId, comment.ModificationDate, comment.ModifierId, comment.Version);
    }
    
    public void SetArticleWarning(PostResult postResult, StringBuilder warningSb)
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
}