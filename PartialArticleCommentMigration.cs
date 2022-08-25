using System.Text.RegularExpressions;
using ForumDataMigration.Enums;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;

namespace ForumDataMigration;

public partial class ArticleCommentMigration
{
    private const string IMAGE_PATTERN = @"\[(?:img|attachimg)](.*?)\[\/(?:img|attachimg)]";
    private const string VIDEO_PATTERN = @"\[(media[^\]]*|video)](.*?)\[\/(media|video)]";
    private const string HIDE_PATTERN = @"(\[\/?hide[^\]]*\]|{[^}]*})";

    private static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeHideTagRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public const string EXTEND_DATA_RECOMMEND_COMMENT = "RecommendComment";
    public const string EXTEND_DATA_BOARD_ID = "BoardId";
    
    private static readonly Dictionary<int, string?> ColorDic = new()
                                                                {
                                                                    { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                                                                };

    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<int, long> CategoryDic = RelationHelper.GetCategoryDic();
    private static readonly Dictionary<(int, string), int> ModDic = ArticleHelper.GetModDic();
    private static readonly Dictionary<long, Read> ReadDic = ArticleHelper.GetReadDic();
    private static readonly CommonSetting CommonSetting = ArticleHelper.GetCommonSetting();
    private static readonly List<Period> Periods = PeriodHelper.GetPeriods();
    private static readonly List<int> PostTableIds = ArticleHelper.GetPostTableIds(150);
    
    private const string COPY_PREFIX = $"COPY \"{nameof(Article)}\" " +
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

    private const string COVER_RELATION_PREFIX = $"COPY \"{nameof(ArticleCoverRelation)}\" " +
                                                 $"(\"{nameof(ArticleCoverRelation.Id)}\",\"{nameof(ArticleCoverRelation.OriginCover)}\",\"{nameof(ArticleCoverRelation.Tid)}\",\"{nameof(ArticleCoverRelation.Pid)}\",\"{nameof(ArticleCoverRelation.AttachmentUrl)}\"" + Setting.COPY_SUFFIX;

    private const string COPY_REWARD_PREFIX = $"COPY \"{nameof(ArticleReward)}\" " +
                                              $"(\"{nameof(ArticleReward.Id)}\",\"{nameof(ArticleReward.Point)}\",\"{nameof(ArticleReward.ExpirationDate)}\"" +
                                              $",\"{nameof(ArticleReward.SolveCommentId)}\",\"{nameof(ArticleReward.SolveDate)}\",\"{nameof(ArticleReward.AllowAdminSolveDate)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_WARNING_PREFIX = $"COPY \"{nameof(Warning)}\" " +
                                               $"(\"{nameof(Warning.Id)}\",\"{nameof(Warning.WarningType)}\",\"{nameof(Warning.SourceId)}\",\"{nameof(Warning.MemberId)}\"" +
                                               $",\"{nameof(Warning.WarnerId)}\",\"{nameof(Warning.Reason)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_COMMENT_PREFIX = $"COPY \"{nameof(Comment)}\" " +
                                               $"(\"{nameof(Comment.Id)}\",\"{nameof(Comment.RootId)}\",\"{nameof(Comment.ParentId)}\",\"{nameof(Comment.Level)}\",\"{nameof(Comment.Hierarchy)}\"" +
                                               $",\"{nameof(Comment.SortingIndex)}\",\"{nameof(Comment.Title)}\",\"{nameof(Comment.Content)}\",\"{nameof(Comment.VisibleType)}\",\"{nameof(Comment.Ip)}\"" +
                                               $",\"{nameof(Comment.Sequence)}\",\"{nameof(Comment.RelatedScore)}\",\"{nameof(Comment.ReplyCount)}\",\"{nameof(Comment.LikeCount)}\"" +
                                               $",\"{nameof(Comment.DislikeCount)}\",\"{nameof(Comment.IsDeleted)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_COMMENT_EXTEND_DATA_PREFIX = $"COPY \"{nameof(CommentExtendData)}\" (\"{nameof(CommentExtendData.Id)}\",\"{nameof(CommentExtendData.Key)}\",\"{nameof(CommentExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;
    
    private const string ARTICLE_JSON = "ArticleJson";
    private const string COMMENT_JSON = "CommentJson";
    private static readonly string ArticleEsIdPrefix = $"{{\"create\":{{ \"_id\": \"{nameof(DocumentType.Thread).ToLower()}-";
    private static readonly string CommentEsIdPrefix = $"{{\"create\":{{ \"_id\": \"{nameof(DocumentType.Comment).ToLower()}-";
    private static readonly string CommentEsRootIdPrefix = $"\",\"routing\": \"{nameof(DocumentType.Thread).ToLower()}-";
    private static readonly string EsIdSuffix = $"\" }}}}";
    private static readonly string ArticleRelationShipName = DocumentType.Thread.ToString().ToLower();
    private static readonly string CommentRelationShipName = DocumentType.Comment.ToString().ToLower();
    private static readonly string CommentRelationShipParentPrefix = DocumentType.Thread.ToString().ToLower() + "-";

    private const string QUERY_ARTICLE_COMMENT_SQL = $@"SELECT 
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
                                      FROM `pre_forum_post{{0}}`
                                      WHERE dateline >= @Start AND dateline < @End --  AND position = 1 AND first = true
                                      -- FROM pre_forum_post_96 where tid = 11229114
                                    ) AS post
                                    LEFT JOIN pre_forum_thread AS thread ON thread.tid = post.tid AND post.first = true AND post.position = 1
                                    LEFT JOIN pre_forum_thankcount AS thankCount ON thankCount.tid = post.tid  AND post.first = true AND post.position = 1
                                    LEFT JOIN pre_post_delay AS postDelay ON postDelay.tid = thread.tid AND post.first = true  AND post.position = 1
                                    LEFT JOIN pre_forum_poststick AS postStick ON postStick.tid = post.tid AND postStick.pid = post.pid
                                    -- LEFT JOIN pre_forum_warning as warning on warning.pid = post.pid
                                    ";
}