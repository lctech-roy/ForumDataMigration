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
                                 $"(\"{nameof(ArticleRating.Id)}\",\"{nameof(ArticleRating.CreationDate)}\",\"{nameof(ArticleRating.CreatorId)}\",\"{nameof(ArticleRating.ModificationDate)}\",\"{nameof(ArticleRating.Version)}\"" +
                                 $",\"{nameof(ArticleRating.ModifierId)}\",\"{nameof(ArticleRating.ArticleId)}\",\"{nameof(ArticleRating.VisibleType)}\",\"{nameof(ArticleRating.Content)}\") " +
                                 $"FROM STDIN (DELIMITER '{Setting.D}')\n{{0}}";

        const string ratingItemSql = $"COPY \"{nameof(ArticleRatingItem)}\" " +
                                     $"(\"{nameof(ArticleRatingItem.Id)}\",\"{nameof(ArticleRatingItem.CreationDate)}\",\"{nameof(ArticleRatingItem.CreatorId)}\",\"{nameof(ArticleRatingItem.ModificationDate)}\",\"{nameof(ArticleRating.Version)}\"" +
                                     $",\"{nameof(ArticleRatingItem.ModifierId)}\",\"{nameof(ArticleRatingItem.CreditId)}\",\"{nameof(ArticleRatingItem.Point)}\") " +
                                     $"FROM STDIN (DELIMITER '{Setting.D}')\n{{0}}";
        
        var periods = PeriodHelper.GetPeriods();

        Parallel.ForEach(periods,
                         period =>
                         {
                             using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                             var rateLogs = cn.Query<RateLog>(queryRateSql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToArray();

                             if (!rateLogs.Any()) return;

                             var ratingDic = new Dictionary<(uint pid, int tid, uint uid), byte>();
                             var ratingValueSb = new StringBuilder();
                             var ratingItemValueSb = new StringBuilder();

                             foreach (var rateLog in rateLogs)
                             {
                                 var rateCreateDate = DateTimeOffset.FromUnixTimeSeconds(rateLog.Dateline);
                                 var ratingId = _snowflake.Generate();

                                 //對不到Tid的不處理
                                 if (!relationDic.ContainsKey(rateLog.Tid))
                                     continue;

                                 if (!ratingDic.ContainsKey((rateLog.Pid, rateLog.Tid, rateLog.Uid)))
                                 {
                                     rateLog.Reason = rateLog.Reason.ToCopyText();

                                     ratingValueSb.Append($"{ratingId}{Setting.D}{rateCreateDate}{Setting.D}{rateLog.Uid}{Setting.D}{rateCreateDate}{Setting.D}{0}{Setting.D}{0}{Setting.D}{relationDic[rateLog.Tid]}{Setting.D}{(int) VisibleType.Public}{Setting.D}{rateLog.Reason}\n");
                                     ratingItemValueSb.Append($"{ratingId}{Setting.D}{rateCreateDate}{Setting.D}{rateLog.Uid}{Setting.D}{rateCreateDate}{Setting.D}{0}{Setting.D}{0}{Setting.D}{rateLog.Extcredits}{Setting.D}{rateLog.Score}\n");

                                     ratingDic.Add((rateLog.Pid, rateLog.Tid, rateLog.Uid), rateLog.Extcredits);
                                 }
                                 else
                                 {
                                     var existsRatingCreditId = ratingDic[(rateLog.Pid, rateLog.Tid, rateLog.Uid)];

                                     if (existsRatingCreditId != rateLog.Extcredits)
                                         ratingItemValueSb.Append($"{ratingId}{Setting.D}{rateCreateDate}{Setting.D}{rateLog.Uid}{Setting.D}{rateCreateDate}{Setting.D}{0}{Setting.D}{0}{Setting.D}{rateLog.Extcredits}{Setting.D}{rateLog.Score}\n");
                                 }
                             }

                             if (ratingValueSb.Length == 0) return;
                             
                             var insertRatingSql = string.Format(ratingSql, ratingValueSb);
                             var insertRatingItemSql = string.Format(ratingItemSql, ratingItemValueSb);

                             File.WriteAllText($"{ratingPath}/{period.FileName}", insertRatingSql);
                             File.WriteAllText($"{ratingItemPath}/{period.FileName}", insertRatingItemSql);
                             
                             Console.WriteLine(period.FileName);
                         });
    }
}