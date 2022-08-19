using System.Text;
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

        var articleDic = RelationContainer.ArticleIdDic;
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

                                     var articleSb = new StringBuilder();
                                     var articleCoverSb = new StringBuilder();
                                     var commentSb = new StringBuilder();
                                     var commentExtendDataSb = new StringBuilder();
                                     var commentReplySb = new StringBuilder();
                                     var warningSb = new StringBuilder();
                                     var articleRewardSb = new StringBuilder();

                                     var rewardDic = new Dictionary<int, ArticleReward>(); //有解決的懸賞文章要暫存

                                     foreach (var post in posts)
                                     {
                                         if (post.Tid == 14615241)
                                         {
                                             
                                         }
                                         
                                        
                                         
                                         //髒資料放過他
                                         if (!articleDic.ContainsKey(post.Tid) || !boardDic.ContainsKey(post.Fid))
                                             continue;

                                         var postResult = new PostResult
                                                          {
                                                              ArticleId = articleDic[post.Tid],
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
                                             //SetArticle(postResult, articleSb, articleCoverSb);
                                             SetArticleReward(postResult, articleRewardSb, rewardDic);
                                             //SetArticleWarning(postResult,warningSb);
                                             //SetCommentFirst(postResult, commentSb, commentExtendDataSb, period, postTableId, commentSql);
                                         }
                                         else if (post.Position != 1) //留言
                                         {
                                             var commentId = _snowflake.Generate();
                                             SetArticleRewardSolved(postResult, articleRewardSb, rewardDic, commentId);
                                             //SetComment(postResult, commentSb, commentExtendDataSb, commentReplySb, period, postTableId, commentSql, commentId);
                                         }
                                     }

                                     if (articleSb.Length > 0)
                                     {
                                         var articlePath = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}/{period.FolderName}";
                                         Directory.CreateDirectory(articlePath);
                                         var fullPath = $"{articlePath}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(articleSql, articleSb.ToString()));

                                         Console.WriteLine(fullPath);
                                     }

                                     if (articleCoverSb.Length > 0)
                                     {
                                         var articleCoverPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}/{period.FolderName}";
                                         Directory.CreateDirectory(articleCoverPath);
                                         var fullPath = $"{articleCoverPath}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(articleCoverRelationSql, articleCoverSb.ToString()));
                                         Console.WriteLine(fullPath);
                                     }

                                     if (articleRewardSb.Length > 0)
                                     {
                                         var articleRewardPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}/{period.FolderName}";
                                         Directory.CreateDirectory(articleRewardPath);
                                         var fullPath = $"{articleRewardPath}/{postTableId}.sql";
                                         File.WriteAllText(fullPath, string.Concat(articleRewardSql, articleRewardSb.ToString()));
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

    private static void SetArticle(PostResult postResult, StringBuilder articleSb, StringBuilder articleCoverSb)
    {
        var post = postResult.Post;

        var isScheduling = post.PostTime < post.Dateline;
        var highlightInt = post.Highlight % 10; //只要取個位數
        var read = postResult.ReadDic.ContainsKey(post.Tid) ? postResult.ReadDic[post.Tid] : null;
        var imageCount = Setting.BbCodeImageRegex.Matches(post.Message).Count;
        var videoCount = Setting.BbCodeVideoRegex.Matches(post.Message).Count;
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
                          Cover = SetArticleCoverRelation(postResult, articleCoverSb)?.Id,
                          Tag = post.Tags.ToNewTags(),
                          RatingCount = post.Ratetimes ?? 0,
                          ShareCount = post.Sharetimes,
                          ImageCount = imageCount,
                          VideoCount = videoCount,
                          DonatePoint = 0,
                          Highlight = post.Highlight != 0,
                          HighlightColor = Setting.ColorDic.ContainsKey(highlightInt) ? Setting.ColorDic[highlightInt] : null,
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
                          HideExpirationDate = Setting.BbCodeHideTagRegex.IsMatch(post.Message!) ? postResult.CreateDate.AddDays(postResult.HideExpirationDay) : null,
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

        articleSb.Append($"{article.Id}{Setting.D}{article.BoardId}{Setting.D}{article.CategoryId}{Setting.D}{(int) article.Status}{Setting.D}{(int) article.VisibleType}{Setting.D}" +
                         $"{(int) article.Type}{Setting.D}{(int) article.ContentType}{Setting.D}{(int) article.PinType}{Setting.D}{article.Title.ToCopyText()}{Setting.D}" +
                         $"{article.Content.ToCopyText()}{Setting.D}{article.ViewCount}{Setting.D}{article.ReplyCount}{Setting.D}{article.SortingIndex}{Setting.D}{article.LastReplyDate.ToCopyValue()}{Setting.D}" +
                         $"{article.LastReplierId.ToCopyValue()}{Setting.D}{article.PinPriority}{Setting.D}" +
                         $"{article.Cover.ToCopyValue()}{Setting.D}{article.Tag}{Setting.D}{article.RatingCount}{Setting.D}{article.ShareCount}{Setting.D}" +
                         $"{article.ImageCount}{Setting.D}{article.VideoCount}{Setting.D}{article.DonatePoint}{Setting.D}{article.Highlight}{Setting.D}{article.HighlightColor.ToCopyValue()}{Setting.D}" +
                         $"{article.Recommend}{Setting.D}{article.ReadPermission}{Setting.D}{article.CommentDisabled}{Setting.D}{(int) article.CommentVisibleType}{Setting.D}{article.LikeCount}{Setting.D}" +
                         $"{article.Ip}{Setting.D}{article.Price}{Setting.D}{article.AuditorId.ToCopyValue()}{Setting.D}{article.AuditFloor.ToCopyValue()}{Setting.D}" +
                         $"{article.SchedulePublishDate.ToCopyValue()}{Setting.D}{article.HideExpirationDate.ToCopyValue()}{Setting.D}{article.PinExpirationDate.ToCopyValue()}{Setting.D}" +
                         $"{article.RecommendExpirationDate.ToCopyValue()}{Setting.D}{article.HighlightExpirationDate.ToCopyValue()}{Setting.D}{article.CommentDisabledExpirationDate.ToCopyValue()}{Setting.D}" +
                         $"{article.InVisibleArticleExpirationDate.ToCopyValue()}{Setting.D}{article.Signature}{Setting.D}{article.Warning}{Setting.D}" +
                         $"{article.CreationDate}{Setting.D}{article.CreatorId}{Setting.D}{article.ModificationDate}{Setting.D}{article.ModifierId}{Setting.D}{article.Version}\n");
    }

    private static ArticleCoverRelation? SetArticleCoverRelation(PostResult postResult, StringBuilder articleCoverSb)
    {
        var post = postResult.Post;

        var isCover = post.Cover is not ("" or "0");
        var coverPath = isCover ? CoverHelper.GetCoverPath(post.Tid, post.Cover) : CoverHelper.GetThumbPath(post.Tid, post.Thumb);

        if (string.IsNullOrEmpty(coverPath)) return null;

        var articleCoverRelation = new ArticleCoverRelation
                                   {
                                       Id = postResult.ArticleId,
                                       OriginCover = isCover ? post.Cover : post.Thumb,
                                       Tid = post.Tid,
                                       Pid = Convert.ToInt32(post.Tid),
                                       AttachmentUrl = isCover ? CoverHelper.GetCoverPath(post.Tid, post.Cover) : CoverHelper.GetThumbPath(post.Tid, post.Thumb)
                                   };

        articleCoverSb.Append($"{articleCoverRelation.Id}{Setting.D}{articleCoverRelation.OriginCover}{Setting.D}{articleCoverRelation.Tid}{Setting.D}{articleCoverRelation.Pid}{Setting.D}{articleCoverRelation.AttachmentUrl}\n");

        return articleCoverRelation;
    }

    private static void SetArticleReward(PostResult postResult, StringBuilder articleRewardSb, IDictionary<int, ArticleReward> rewardDic)
    {
        var post = postResult.Post;

        if (post.Special != 3) return;

        var articleReward = new ArticleReward
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
            articleRewardSb.Append($"{articleReward.Id}{Setting.D}{articleReward.Point}{Setting.D}{articleReward.ExpirationDate}{Setting.D}" +
                                   $"{articleReward.SolveCommentId.ToCopyValue()}{Setting.D}{articleReward.SolveDate.ToCopyValue()}{Setting.D}{articleReward.AllowAdminSolveDate}{Setting.D}" +
                                   $"{articleReward.CreationDate}{Setting.D}{articleReward.CreatorId}{Setting.D}{articleReward.ModificationDate}{Setting.D}{articleReward.ModifierId}{Setting.D}{articleReward.Version}\n");
        }
        else
        {
            rewardDic.Add(post.Tid, articleReward);
        }
    }

    private static void SetArticleRewardSolved(PostResult postResult, StringBuilder articleRewardSb, IDictionary<int, ArticleReward> rewardDic, long commentId)
    {
        var post = postResult.Post;

        if (!rewardDic.ContainsKey(post.Tid) || rewardDic[post.Tid].CreationDate.AddSeconds(1) != postResult.CreateDate) return;

        var articleReward = rewardDic[post.Tid];
        articleReward.Point = Math.Abs(articleReward.Point);
        articleReward.SolveDate = postResult.CreateDate;
        articleReward.SolveCommentId = commentId;

        articleRewardSb.Append($"{articleReward.Id}{Setting.D}{articleReward.Point}{Setting.D}{articleReward.ExpirationDate}{Setting.D}" +
                               $"{articleReward.SolveCommentId.ToCopyValue()}{Setting.D}{articleReward.SolveDate.ToCopyValue()}{Setting.D}{articleReward.AllowAdminSolveDate}{Setting.D}" +
                               $"{articleReward.CreationDate}{Setting.D}{articleReward.CreatorId}{Setting.D}{articleReward.ModificationDate}{Setting.D}{articleReward.ModifierId}{Setting.D}{articleReward.Version}\n");

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

        AppendCommentSb(comment, commentSb, period, postTableId, commentSql);

        commentExtendDataSb.Append($"{postResult.ArticleId}{Setting.D}{Setting.EXTEND_DATA_BOARD_ID}{Setting.D}{postResult.BoardId}{Setting.D}" +
                                   $"{comment.CreationDate}{Setting.D}{comment.CreatorId}{Setting.D}{comment.ModificationDate}{Setting.D}{comment.ModifierId}{Setting.D}{comment.Version}\n");
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

        AppendCommentSb(comment, commentSb, period, postTableId, commentSql);

        if (post.StickDateline.HasValue)
        {
            var stickDate = DateTimeOffset.FromUnixTimeSeconds(post.StickDateline.Value);

            commentExtendDataSb.Append($"{commentId}{Setting.D}{Setting.EXTEND_DATA_RECOMMEND_COMMENT}{Setting.D}{true}{Setting.D}" +
                                       $"{stickDate}{Setting.D}{0}{Setting.D}{stickDate}{Setting.D}{0}{Setting.D}{0}\n");
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

            commentReplySb.Append($"{commentReply.Id}{Setting.D}{commentReply.RootId}{Setting.D}{commentReply.ParentId}{Setting.D}{commentReply.Level}{Setting.D}{commentReply.Hierarchy}{Setting.D}{commentReply.SortingIndex}{Setting.D}" +
                                  $"{commentReply.Content.ToCopyText()}{Setting.D}{(int) commentReply.VisibleType}{Setting.D}{commentReply.Ip}{Setting.D}{commentReply.Sequence}{Setting.D}" +
                                  $"{commentReply.RelatedScore}{Setting.D}{commentReply.ReplyCount}{Setting.D}{commentReply.LikeCount}{Setting.D}{commentReply.DislikeCount}{Setting.D}{commentReply.IsDeleted}{Setting.D}" +
                                  $"{commentReply.CreationDate}{Setting.D}{commentReply.CreatorId}{Setting.D}{commentReply.ModificationDate}{Setting.D}{commentReply.ModifierId}{Setting.D}{commentReply.Version}\n");
        }
    }

    private static void AppendCommentSb(Comment comment, StringBuilder commentSb, Period period, int postTableId, string commentSql)
    {
        var commentAppendStr = $"{comment.Id}{Setting.D}{comment.RootId}{Setting.D}{comment.ParentId.ToCopyValue()}{Setting.D}{comment.Level}{Setting.D}{comment.Hierarchy}{Setting.D}{comment.SortingIndex}{Setting.D}" +
                               $"{comment.Content.ToCopyText()}{Setting.D}{(int) comment.VisibleType}{Setting.D}{comment.Ip}{Setting.D}{comment.Sequence}{Setting.D}" +
                               $"{comment.RelatedScore}{Setting.D}{comment.ReplyCount}{Setting.D}{comment.LikeCount}{Setting.D}{comment.DislikeCount}{Setting.D}{comment.IsDeleted}{Setting.D}" +
                               $"{comment.CreationDate}{Setting.D}{comment.CreatorId}{Setting.D}{comment.ModificationDate}{Setting.D}{comment.ModifierId}{Setting.D}{comment.Version}\n";

        try
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

                commentSb = new StringBuilder(commentAppendStr);
            }
            else
            {
                commentSb.Append(commentAppendStr);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            throw;
        }
    }

    public void SetArticleWarning(PostResult postResult, StringBuilder warningSb)
    {
        var post = postResult.Post;

        if (post.Warning != null)
        {
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

            warningSb.Append($"{warning.Id}{Setting.D}{(int) warning.WarningType}{Setting.D}{warning.SourceId}{Setting.D}{warning.MemberId}{Setting.D}{warning.WarnerId}{Setting.D}{warning.Reason}{Setting.D}" +
                             $"{warning.CreationDate}{Setting.D}{warning.CreatorId}{Setting.D}{warning.ModificationDate}{Setting.D}{warning.ModifierId}{Setting.D}{warning.Version}\n");
        }
    }
}