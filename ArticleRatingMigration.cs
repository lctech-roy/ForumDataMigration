using System.Collections.Concurrent;
using System.Text;
using Netcorext.Algorithms;
using Dapper;
using ForumDataMigration.Enums;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Lctech.Jkf.Domain.Enums;
using MySqlConnector;

namespace ForumDataMigration;

public class ArticleRatingMigration
{
    private readonly ISnowflake _snowflake;

    private const string QUERY_RATE_SQL = @"SELECT thread.fid, thread.typeid, rate.tid, rate.uid,rate.extcredits,
			                                SUM(rate.score) AS score,
			                                MAX(rate.reason) AS reason,
			                                MIN(rate.dateline) AS dateline FROM pre_forum_post{0} AS post 
                                            INNER JOIN pre_forum_thread AS thread ON thread.tid = post.tid
                                            INNER JOIN pre_forum_ratelog AS rate ON rate.tid = post.tid AND rate.pid = post.pid
                                            WHERE post.`first` = TRUE AND post.`position` = 1";

    private const string QUERY_RATE_SQL_DATE_CONDITION = " AND post.dateline >= @Start AND post.dateline < @End";
    
    private const string QUERY_RATE_SQL_GROUP = " GROUP BY rate.tid,rate.uid,rate.extcredits";

    private const string COPY_RATING_SQL = $"COPY \"{nameof(ArticleRating)}\" " +
                                           $"(\"{nameof(ArticleRating.Id)}\",\"{nameof(ArticleRating.ArticleId)}\",\"{nameof(ArticleRating.VisibleType)}\",\"{nameof(ArticleRating.Content)}\"" +
                                           Setting.COPY_ENTITY_SUFFIX;

    private const string COPY_RATING_ITEM_SQL = $"COPY \"{nameof(ArticleRatingItem)}\" " +
                                                $"(\"{nameof(ArticleRatingItem.Id)}\",\"{nameof(ArticleRatingItem.CreditId)}\",\"{nameof(ArticleRatingItem.Point)}\"" +
                                                Setting.COPY_ENTITY_SUFFIX;

    private const string POST0_RATING_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}/0";
    private const string POST0_RATING_JASON_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{ARTICLE_RATING_JSON}/0";
    private const string POST0_RATING_ITEM_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}/0";

    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<int, long> CategoryDic = RelationHelper.GetCategoryDic();
    private static readonly ConcurrentDictionary<(int tid, int uid), (long, List<byte>)> RatingIdDic = new();

    private const string ARTICLE_RATING_JSON = "ArticleRatingJson";
    private static readonly string RateEsIdPrefix = $"{{\"create\":{{ \"_id\": \"{nameof(DocumentType.Rating).ToLower()}-";
    private static readonly string RateEsRootIdPrefix = $"\",\"routing\": \"{nameof(DocumentType.Thread).ToLower()}-";
    private static readonly string RateEsRootIdSuffix = $"\" }}}}";
    private static readonly string RelationShipName = DocumentType.Rating.ToString().ToLower();
    private static readonly string RelationShipParentPrefix = DocumentType.Thread.ToString().ToLower() + "-";
    
    public ArticleRatingMigration(ISnowflake snowflake)
    {
        Directory.CreateDirectory(POST0_RATING_DIRECTORY_PATH);
        Directory.CreateDirectory(POST0_RATING_JASON_DIRECTORY_PATH);
        Directory.CreateDirectory(POST0_RATING_ITEM_DIRECTORY_PATH);

        _snowflake = snowflake;
    }

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        var periods = PeriodHelper.GetPeriods();

        var post0Sql = string.Concat(string.Format(QUERY_RATE_SQL, ""), QUERY_RATE_SQL_DATE_CONDITION, QUERY_RATE_SQL_GROUP);

        //pre_forum_post_0 資料最多用日期並行處理
        await Parallel.ForEachAsync(periods, cancellationToken, async (period, token) =>
                                                                {
                                                                    await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                    var rateLogs = (await cn.QueryAsync<RateLog>(new CommandDefinition(post0Sql, new { Start = period.StartSeconds, End = period.EndSeconds }, cancellationToken: token))).ToArray();

                                                                    await ExecuteAsync(rateLogs, 0, period, cancellationToken);
                                                                });

        var postTableIds = ArticleHelper.GetPostTableIds();
        postTableIds.RemoveAt(0);

        await Parallel.ForEachAsync(postTableIds, cancellationToken, async (postTableId, token) =>
                                                                     {
                                                                         var sql = string.Concat(string.Format(QUERY_RATE_SQL, $"_{postTableId}"),QUERY_RATE_SQL_GROUP);

                                                                         await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                         var rateLogs = (await cn.QueryAsync<RateLog>(new CommandDefinition(sql, cancellationToken: token))).ToArray();

                                                                         await ExecuteAsync(rateLogs, postTableId, cancellationToken: cancellationToken);
                                                                     });

        await FileHelper.CombineMultipleFilesIntoSingleFileAsync($"{Setting.INSERT_DATA_PATH}/{ARTICLE_RATING_JSON}",
                                                                 "*.json",
                                                                 $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}.json",
                                                                 cancellationToken);
    }

    private async Task ExecuteAsync(RateLog[] rateLogs, int postTableId, Period? period = null, CancellationToken cancellationToken = default)
    {
        if (!rateLogs.Any()) return;

        var idDic = await RelationHelper.GetArticleIdDicAsync(rateLogs.Select(x => x.Tid).Distinct().ToArray(), cancellationToken);

        if (!idDic.Any())
            return;

        var simpleMemberDic = await RelationHelper.GetSimpleMemberDicAsync(rateLogs.Select(x => x.Uid).Distinct().ToArray(), cancellationToken);

        var ratingSb = new StringBuilder();
        var ratingJsonSb = new StringBuilder();
        var ratingItemSb = new StringBuilder();
        
        foreach (var rateLog in rateLogs)
        {
            //對不到Tid的不處理
            if (!idDic.ContainsKey(rateLog.Tid))
                continue;
            
            var rateCreateDate = DateTimeOffset.FromUnixTimeSeconds(rateLog.Dateline);

            var (memberId, memberName) = simpleMemberDic.ContainsKey(rateLog.Uid) ? simpleMemberDic[rateLog.Uid] : (0, string.Empty);
            
            if (!RatingIdDic.ContainsKey((rateLog.Tid, rateLog.Uid)))
            {
                var ratingId = _snowflake.Generate();

                RatingIdDic.TryAdd((rateLog.Tid, rateLog.Uid), (ratingId, new List<byte>{rateLog.Extcredits}));

                var id = idDic[rateLog.Tid];

                var rating = new ArticleRating()
                             {
                                 Id = ratingId,
                                 ArticleId = id,
                                 VisibleType = VisibleType.Public,
                                 Content = rateLog.Reason,
                                 CreationDate = rateCreateDate,
                                 CreatorId = memberId,
                                 ModificationDate = rateCreateDate,
                                 ModifierId = memberId,
                             };

                ratingSb.AppendValueLine(rating.Id, rating.ArticleId, (int) rating.VisibleType, rating.Content.ToCopyText(),
                                          rating.CreationDate, rating.CreatorId, rating.ModificationDate, rating.ModifierId, rating.Version);

                #region Es文件檔

                ratingJsonSb.Append(RateEsIdPrefix).Append(ratingId).Append(RateEsRootIdPrefix).Append(id).AppendLine(RateEsRootIdSuffix);

                var ratingDocumnet = SetRatingDocument(rating, rateLog, memberName);
                
                var ratingJson = await JsonHelper.GetJsonAsync(ratingDocumnet, cancellationToken);
                
                ratingJsonSb.AppendLine(ratingJson);

                #endregion
              

                var ratingItem = new ArticleRatingItem()
                                 {
                                     Id = ratingId,
                                     CreditId = rateLog.Extcredits,
                                     Point = rateLog.Score,
                                     CreationDate = rateCreateDate,
                                     CreatorId = memberId,
                                     ModificationDate = rateCreateDate,
                                     ModifierId = memberId
                                 };

                ratingItemSb.AppendValueLine(ratingItem.Id, ratingItem.CreditId, ratingItem.Point,
                                              ratingItem.CreationDate, ratingItem.CreatorId, ratingItem.ModificationDate, ratingItem.ModifierId, ratingItem.Version);
            }
            else
            {
                var (existsRatingId, existsRatingCreditIds) = RatingIdDic[(rateLog.Tid, rateLog.Uid)];

                if (existsRatingCreditIds.Contains(rateLog.Extcredits)) continue;

                existsRatingCreditIds.Add(rateLog.Extcredits);
                
                var ratingItem = new ArticleRatingItem()
                                 {
                                     Id = existsRatingId,
                                     CreditId = rateLog.Extcredits,
                                     Point = rateLog.Score,
                                     CreationDate = rateCreateDate,
                                     CreatorId = memberId,
                                     ModificationDate = rateCreateDate,
                                     ModifierId = memberId
                                 };

                ratingItemSb.AppendValueLine(ratingItem.Id, ratingItem.CreditId, ratingItem.Point,
                                              ratingItem.CreationDate, ratingItem.CreatorId, ratingItem.ModificationDate, ratingItem.ModifierId, ratingItem.Version);
            }
        }

        if (ratingSb.Length == 0) return;

        var ratingPath = postTableId == 0 ? $"{POST0_RATING_DIRECTORY_PATH}/{period!.FileName}" : $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}/{postTableId}.sql";
        var ratingJsonPath = postTableId == 0 ? $"{POST0_RATING_JASON_DIRECTORY_PATH}/{period!.FolderName}.json" : $"{Setting.INSERT_DATA_PATH}/{ARTICLE_RATING_JSON}/{postTableId}.json";
        var ratingItemPath = postTableId == 0 ? $"{POST0_RATING_ITEM_DIRECTORY_PATH}/{period!.FileName}" : $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}/{postTableId}.sql";

        var ratingTask = new Task(() =>
                                  {
                                      var insertRatingSql = string.Concat(COPY_RATING_SQL, ratingSb);
                                      File.WriteAllText(ratingPath, insertRatingSql);
                                  });

        var ratingJsonTask = new Task(() => File.WriteAllText(ratingJsonPath, ratingJsonSb.ToString()));

        var ratingItemTask = new Task(() =>
                                      {
                                          var insertRatingItemSql = string.Concat(COPY_RATING_ITEM_SQL, ratingItemSb);
                                          File.WriteAllText(ratingItemPath, insertRatingItemSql);
                                      });

        ratingTask.Start();
        ratingJsonTask.Start();
        ratingItemTask.Start();
        
        await Task.WhenAll(ratingTask, ratingJsonTask, ratingItemTask);
        
        Console.WriteLine(ratingPath);
        Console.WriteLine(ratingJsonPath);
        Console.WriteLine(ratingItemPath);
    }

    private static Document SetRatingDocument(ArticleRating rating,RateLog rateLog, string memberName)
    {
        return new Document()
               {
                   //rating part
                   Id = rating.Id,
                   VisibleType = (int) rating.VisibleType,
                   Content = rating.Content,
                   CreationDate = rating.CreationDate,
                   CreatorId = rating.CreatorId,
                   ModificationDate = rating.ModificationDate,
                   ModifierId = rating.ModifierId,

                   //document part
                   Type = DocumentType.Rating,
                   RootId = rating.ArticleId,
                   BoardId = BoardDic.ContainsKey(rateLog.Fid) ? BoardDic[rateLog.Fid] : 0,
                   CategoryId = CategoryDic.ContainsKey(rateLog.Typeid) ? CategoryDic[rateLog.Typeid] : 0,
                   CreatorUid = rateLog.Uid,
                   CreatorName = memberName,
                   ModifierUid = rateLog.Uid,
                   ModifierName = memberName,
                   Relationship = new Relationship()
                                  {
                                      Name = RelationShipName,
                                      Parent = RelationShipParentPrefix + rating.ArticleId
                                  }
               };
    }
}