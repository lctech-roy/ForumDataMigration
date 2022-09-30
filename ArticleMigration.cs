using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Enums;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Domain.Entities;
using Lctech.Jkf.Forum.Enums;
using MySqlConnector;
using Polly;

namespace ForumDataMigration;

public class ArticleMigration
{
    private const string COPY_PREFIX = $"COPY \"{nameof(Article)}\" " +
                                       $"(\"{nameof(Article.Id)}\",\"{nameof(Article.BoardId)}\",\"{nameof(Article.CategoryId)}\",\"{nameof(Article.Status)}\",\"{nameof(Article.DeleteStatus)}\"" +
                                       $",\"{nameof(Article.VisibleType)}\",\"{nameof(Article.Type)}\",\"{nameof(Article.ContentType)}\",\"{nameof(Article.PinType)}\"" +
                                       $",\"{nameof(Article.Title)}\",\"{nameof(Article.Content)}\",\"{nameof(Article.ViewCount)}\",\"{nameof(Article.ReplyCount)}\"" +
                                       $",\"{nameof(Article.SortingIndex)}\",\"{nameof(Article.LastReplyDate)}\",\"{nameof(Article.LastReplierId)}\",\"{nameof(Article.PinPriority)}\"" +
                                       $",\"{nameof(Article.Cover)}\",\"{nameof(Article.Tag)}\",\"{nameof(Article.RatingCount)}\"" +
                                       $",\"{nameof(Article.ShareCount)}\",\"{nameof(Article.ImageCount)}\",\"{nameof(Article.VideoCount)}\",\"{nameof(Article.DonatePoint)}\"" +
                                       $",\"{nameof(Article.Highlight)}\",\"{nameof(Article.HighlightColor)}\",\"{nameof(Article.Recommend)}\",\"{nameof(Article.ReadPermission)}\"" +
                                       $",\"{nameof(Article.CommentDisabled)}\",\"{nameof(Article.CommentVisibleType)}\",\"{nameof(Article.LikeCount)}\",\"{nameof(Article.Ip)}\"" +
                                       $",\"{nameof(Article.Price)}\",\"{nameof(Article.AuditorId)}\",\"{nameof(Article.AuditFloor)}\",\"{nameof(Article.PublishDate)}\"" +
                                       $",\"{nameof(Article.HideExpirationDate)}\",\"{nameof(Article.PinExpirationDate)}\",\"{nameof(Article.RecommendExpirationDate)}\",\"{nameof(Article.HighlightExpirationDate)}\"" +
                                       $",\"{nameof(Article.CommentDisabledExpirationDate)}\",\"{nameof(Article.InVisibleArticleExpirationDate)}\",\"{nameof(Article.Signature)}\",\"{nameof(Article.Warning)}\"" +
                                       Setting.COPY_ENTITY_SUFFIX;

    private const string COVER_RELATION_PREFIX = $"COPY \"{nameof(ArticleCoverRelation)}\" " +
                                                 $"(\"{nameof(ArticleCoverRelation.Id)}\",\"{nameof(ArticleCoverRelation.OriginCover)}\",\"{nameof(ArticleCoverRelation.Tid)}\",\"{nameof(ArticleCoverRelation.Pid)}\",\"{nameof(ArticleCoverRelation.AttachmentUrl)}\"" + Setting.COPY_SUFFIX;

    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<int, long?> CategoryDic = RelationHelper.GetCategoryDic();
    private static readonly Dictionary<(int, string), int?> ModDic = ArticleHelper.GetModDic();
    private static readonly Dictionary<long, Read> ReadDic = ArticleHelper.GetReadDic();

    private static readonly Dictionary<int, string?> ColorDic = new()
                                                                {
                                                                    { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                                                                };

    private static readonly CommonSetting CommonSetting = ArticleHelper.GetCommonSetting();
    private static readonly Dictionary<int, long> ArticleDic = RelationHelper.GetArticleDic();
    private static readonly Dictionary<long, (long, string)> MemberDIc = RelationHelper.GetSimpleMemberDic();

    private const string IMAGE_PATTERN = @"\[(?:img|attachimg)](.*?)\[\/(?:img|attachimg)]";
    private const string VIDEO_PATTERN = @"\[(media[^\]]*|video)](.*?)\[\/(media|video)]";
    private const string HIDE_PATTERN = @"(\[\/?hide[^\]]*\]|{[^}]*})";

    private static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeHideTagRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string ARTICLE_JSON = "ArticleJson";

    private const string ARTICLE_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}";
    private const string ARTICLE_JSON_PATH = $"{Setting.INSERT_DATA_PATH}/{ARTICLE_JSON}";
    private const string ARTICLE_COVER_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleCoverRelation)}";
    private const string ARTICLE_COMBINE_JSON_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}.json";

    private static readonly string ArticleEsIdPrefix = $"{{\"create\":{{ \"_id\": \"{nameof(DocumentType.Thread).ToLower()}-";
    private static readonly string ArticleEsIdSuffix = $"\" }}}}";
    private static readonly string ArticleRelationShipName = DocumentType.Thread.ToString().ToLower();

    private const string QUERY_ARTICLE_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                               SELECT thread.tid,thread.displayorder,thread.special
                                              ,postDelay.post_time AS postTime,thread.subject,post.message,thread.views
                                              ,thread.replies,thread.fid,thread.typeid,post.dateline
                                              ,thread.lastpost,postReply.authorid AS lastposter
                                              ,thread.cover,thread.thumb,post.tags,post.ratetimes,thread.sharetimes
                                              ,thread.highlight,thread.digest,thread.readperm,thread.closed
                                              ,thread.status,thankCount.count AS thankCount,post.useip,post.usesig
                                              ,thread.price,thread.authorid
                                              FROM pre_forum_thread AS thread
                                              LEFT JOIN pre_forum_post{0} post ON post.first = TRUE AND post.position = 1 AND post.tid = thread.tid
                                              LEFT JOIN pre_forum_post{0} postReply ON thread.replies > 0 AND postReply.tid = thread.tid AND postReply.dateline = thread.lastpost AND postReply.author = thread.lastposter
                                              LEFT JOIN pre_post_delay AS postDelay ON postDelay.tid = thread.tid
                                              LEFT JOIN pre_forum_thankcount AS thankCount ON thankCount.tid = thread.tid
                                              WHERE thread.posttableid = @postTableId AND thread.dateline >= @Start AND thread.dateline < @End AND post.tid is not null";

    //WHERE thread.posttableid = @postTableId AND thread.tid = 8128927";
    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        RetryHelper.CreateArticleRetryTable();
        var (folderName, _) = RetryHelper.GetArticleRetry();
        var postTableIds = ArticleHelper.GetPostTableIds();
        var periods = PeriodHelper.GetPeriods(folderName);

        if (folderName != null)
            RetryHelper.RemoveFilesByDate(new[] { ARTICLE_PATH, ARTICLE_JSON_PATH, ARTICLE_COVER_PATH }, folderName);
        else
            RetryHelper.RemoveFiles(new[] { ARTICLE_PATH, ARTICLE_JSON_PATH, ARTICLE_COVER_PATH, ARTICLE_COMBINE_JSON_PATH });

        foreach (var period in periods)
        {
            await Parallel.ForEachAsync(postTableIds, CommonHelper.GetParallelOptions(cancellationToken), async (postTableId, token) =>
                                                                                                          {
                                                                                                              var posts = Array.Empty<ArticlePost>();

                                                                                                              var sql = string.Concat(string.Format(QUERY_ARTICLE_SQL, postTableId == 0 ? "" : $"_{postTableId}"));

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
                                                                                                                           .ExecuteAsync(async () => { posts = (await cn.QueryAsync<ArticlePost>(command)).ToArray(); });
                                                                                                                  }

                                                                                                                  if (!posts.Any())
                                                                                                                      return;

                                                                                                                  await ExecuteAsync(posts, postTableId, period, cancellationToken);
                                                                                                              }
                                                                                                              catch (Exception e)
                                                                                                              {
                                                                                                                  Console.WriteLine(e);
                                                                                                                  await File.AppendAllTextAsync($"{Setting.INSERT_DATA_PATH}/Error.txt", $"{period.FolderName}{Environment.NewLine}{e}", token);
                                                                                                                  RetryHelper.SetArticleRetry(period.FolderName, null, e.ToString());

                                                                                                                  throw;
                                                                                                              }
                                                                                                          });
        }

        await FileHelper.CombineMultipleFilesIntoSingleFileAsync(ARTICLE_JSON_PATH,
                                                                 "*.json",
                                                                 ARTICLE_COMBINE_JSON_PATH,
                                                                 cancellationToken);
    }

    private static async Task ExecuteAsync(ArticlePost[] posts, int postTableId, Period? period = null, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var jsonSb = new StringBuilder();
        var coverSb = new StringBuilder();

        // var sw = new Stopwatch();
        // sw.Start();
        var attachPathDic = await RegexHelper.GetAttachFileNameDicAsync(RegexHelper.GetAttachmentGroups(posts), cancellationToken);

        // sw.Stop();
        // Console.WriteLine($"selectMany Time => {sw.ElapsedMilliseconds}ms");

        long previousId = 0;

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

            // 排除因為lastPoster重複的文章
            if (id == previousId)
                continue;

            previousId = id;

            var postResult = new ArticlePostResult()
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

            try
            {
                await SetArticleAsync(postResult, sb, coverSb, jsonSb, cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Tid:{post.Tid} Pid:{post.Pid}");
                Console.WriteLine(e);

                throw;
            }

            //await CommonHelper.WatchTimeAsync("SetArticleAsync", async () => await SetArticleAsync(postResult, sb, coverSb, jsonSb, cancellationToken));
        }

        var task = new Task(() => { WriteToFile($"{ARTICLE_PATH}/{postTableId}", period!.FileName, COPY_PREFIX, sb); });

        var jsonTask = new Task(() => { WriteToFile($"{ARTICLE_JSON_PATH}/{postTableId}", $"{period!.FolderName}.json", "", jsonSb); });

        var coverTask = new Task(() => { WriteToFile($"{ARTICLE_COVER_PATH}/{postTableId}", period!.FileName, COVER_RELATION_PREFIX, coverSb); });

        task.Start();
        jsonTask.Start();
        coverTask.Start();

        await Task.WhenAll(task, jsonTask, coverTask);
    }

    private static async Task SetArticleAsync(ArticlePostResult postResult, StringBuilder sb, StringBuilder coverSb, StringBuilder jsonSb, CancellationToken cancellationToken)
    {
        var post = postResult.Post;

        var isScheduling = post.PostTime < post.Dateline;
        var highlightInt = post.Highlight % 10; //只要取個位數
        var read = ReadDic.GetValueOrDefault(post.Tid);
        var imageCount = BbCodeImageRegex.Matches(post.Message).Count;
        var videoCount = BbCodeVideoRegex.Matches(post.Message).Count;

        var article = new Article
                      {
                          Id = postResult.ArticleId,
                          Status =
                              post.Displayorder == -2 ? ArticleStatus.Pending :
                              post.Displayorder == -3 ? ArticleStatus.Hide : //待確認
                              post.Displayorder == -4 ? ArticleStatus.Hide :
                              isScheduling ? ArticleStatus.Scheduling : ArticleStatus.Published,
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
                          LastReplierId = post.Lastposter,
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
                          Warning = post.Warning != null,
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
                           article.InVisibleArticleExpirationDate.ToCopyValue(), article.Signature, article.Warning,
                           article.CreationDate, article.CreatorId, article.ModificationDate, article.ModifierId, article.Version);

        #region Es文件檔

        jsonSb.Append(ArticleEsIdPrefix).Append(article.Id).AppendLine(ArticleEsIdSuffix);

        var document = SetArticleDocument(article, postResult);

        var json = await JsonHelper.GetJsonAsync(document, cancellationToken);

        jsonSb.AppendLine(json);

        #endregion
    }

    private static ArticleCoverRelation? SetArticleCoverRelation(ArticlePostResult postResult, StringBuilder coverSb)
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

    private static Document SetArticleDocument(Article article, ArticlePostResult postResult)
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

    private static void WriteToFile(string directoryPath, string fileName, string copyPrefix, StringBuilder valueSb)
    {
        if (valueSb.Length == 0)
            return;

        var fullPath = $"{directoryPath}/{fileName}";

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(fullPath, string.Concat(copyPrefix, valueSb.ToString()));
        Console.WriteLine(fullPath);
        valueSb.Clear();
    }
}