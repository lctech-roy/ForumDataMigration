using System.Text;
using Netcorext.Algorithms;
using Netcorext.EntityFramework.UserIdentityPattern;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Lctech.Jkf.Domain.Enums;

namespace ForumDataMigration;

public class ArticleRatingMigration
{
    private readonly ISnowflake _snowflake;

    public ArticleRatingMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        var relationDic = RelationContainer.ArticleIdDic;

        const string ratingPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}";
        Directory.CreateDirectory(ratingPath);

        const string ratingItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}";
        Directory.CreateDirectory(ratingItemPath);

        const string queryRateSql = @"SELECT tid,pid,uid,extcredits,dateline,score,reason,forceshow FROM pre_forum_ratelog WHERE tid<>-1 AND dateline >= @Start AND dateline < @End";

        const string ratingSql = $"COPY \"{nameof(ArticleRating)}\" " +
                                 $"(\"{nameof(ArticleRating.Id)}\",\"{nameof(ArticleRating.ArticleId)}\",\"{nameof(ArticleRating.VisibleType)}\",\"{nameof(ArticleRating.Content)}\"" +
                                 Setting.COPY_SUFFIX;

        const string ratingItemSql = $"COPY \"{nameof(ArticleRatingItem)}\" " +
                                     $"(\"{nameof(ArticleRatingItem.Id)}\",\"{nameof(ArticleRatingItem.CreditId)}\",\"{nameof(ArticleRatingItem.Point)}\"" +
                                     Setting.COPY_SUFFIX;
        
        var periods = PeriodHelper.GetPeriods();

        Parallel.ForEach(periods,
                         period =>
                         {
                             using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                             var rateLogs = cn.Query<RateLog>(queryRateSql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToArray();

                             if (!rateLogs.Any()) return;

                             var ratingIdDic = new Dictionary<(uint pid, int tid, uint uid), byte>();
                             var articleRatingSb = new StringBuilder();
                             var articleRatingItemSb = new StringBuilder();

                             foreach (var rateLog in rateLogs)
                             {
                                 var rateCreateDate = DateTimeOffset.FromUnixTimeSeconds(rateLog.Dateline);
                                 var ratingId = _snowflake.Generate();

                                 //對不到Tid的不處理
                                 if (!relationDic.ContainsKey(rateLog.Tid))
                                     continue;

                                 if (!ratingIdDic.ContainsKey((rateLog.Pid, rateLog.Tid, rateLog.Uid)))
                                 {
                                     var articleRating = new ArticleRating
                                                         {
                                                             Id = ratingId,
                                                             ArticleId = relationDic[rateLog.Tid],
                                                             VisibleType = VisibleType.Public,
                                                             Content = rateLog.Reason,
                                                             CreationDate = rateCreateDate,
                                                             CreatorId = rateLog.Uid,
                                                             ModificationDate = rateCreateDate,
                                                             ModifierId = rateLog.Uid
                                                         };

                                     articleRatingSb.Append($"{articleRating.Id}{Setting.D}{articleRating.ArticleId}{Setting.D}{(int) articleRating.VisibleType}{Setting.D}{articleRating.Content.ToCopyText()}{Setting.D}" +
                                                            $"{articleRating.CreationDate}{Setting.D}{articleRating.CreatorId}{Setting.D}{articleRating.ModificationDate}{Setting.D}{articleRating.ModifierId}{Setting.D}{articleRating.Version}\n");
                                     
                                     var articleRatingItem = new ArticleRatingItem()
                                                         {
                                                             Id = ratingId,
                                                             CreditId = rateLog.Extcredits,
                                                             Point = rateLog.Score,
                                                             CreationDate = rateCreateDate,
                                                             CreatorId = rateLog.Uid,
                                                             ModificationDate = rateCreateDate,
                                                             ModifierId = rateLog.Uid
                                                         };
                                     
                                     articleRatingItemSb.Append($"{articleRatingItem.Id}{Setting.D}{articleRatingItem.CreditId}{Setting.D}{articleRatingItem.Point}{Setting.D}" +
                                                            $"{articleRatingItem.CreationDate}{Setting.D}{articleRatingItem.CreatorId}{Setting.D}{articleRatingItem.ModificationDate}{Setting.D}{articleRatingItem.ModifierId}{Setting.D}{articleRatingItem.Version}\n");
                                     
                                     ratingIdDic.Add((rateLog.Pid, rateLog.Tid, rateLog.Uid), rateLog.Extcredits);
                                 }
                                 else
                                 {
                                     var existsRatingCreditId = ratingIdDic[(rateLog.Pid, rateLog.Tid, rateLog.Uid)];

                                     if (existsRatingCreditId == rateLog.Extcredits) continue;

                                     var articleRatingItem = new ArticleRatingItem()
                                                             {
                                                                 Id = ratingId,
                                                                 CreditId = rateLog.Extcredits,
                                                                 Point = rateLog.Score,
                                                                 CreationDate = rateCreateDate,
                                                                 CreatorId = rateLog.Uid,
                                                                 ModificationDate = rateCreateDate,
                                                                 ModifierId = rateLog.Uid
                                                             };
                                         
                                     articleRatingItemSb.Append($"{articleRatingItem.Id}{Setting.D}{articleRatingItem.CreditId}{Setting.D}{articleRatingItem.Point}{Setting.D}" +
                                                                $"{articleRatingItem.CreationDate}{Setting.D}{articleRatingItem.CreatorId}{Setting.D}{articleRatingItem.ModificationDate}{Setting.D}{articleRatingItem.ModifierId}{Setting.D}{articleRatingItem.Version}\n");
                                 }
                             }

                             if (articleRatingSb.Length == 0) return;

                             var insertRatingSql = string.Concat(ratingSql, articleRatingSb);
                             var insertRatingItemSql = string.Concat(ratingItemSql, articleRatingItemSb);

                             File.WriteAllText($"{ratingPath}/{period.FileName}", insertRatingSql);
                             File.WriteAllText($"{ratingItemPath}/{period.FileName}", insertRatingItemSql);
                             
                             Console.WriteLine(period.FileName);
                         });
    }
}