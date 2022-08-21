using System.Text;
using Netcorext.Algorithms;
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

    private const string QUERY_RATE_SQL = @"SELECT rate.* FROM pre_forum_post{0} AS post 
                                      INNER JOIN pre_forum_thread AS thread ON thread.tid = post.tid
                                      INNER JOIN pre_forum_ratelog AS rate ON rate.tid = post.tid AND rate.pid = post.pid
                                      WHERE post.`first` = TRUE";

    private const string QUERY_RATE_SQL_DATE_CONDITION = " AND post.dateline >= @Start AND post.dateline < @End";

    private const string RATING_SQL = $"COPY \"{nameof(ArticleRating)}\" " +
                                      $"(\"{nameof(ArticleRating.Id)}\",\"{nameof(ArticleRating.ArticleId)}\",\"{nameof(ArticleRating.VisibleType)}\",\"{nameof(ArticleRating.Content)}\"" +
                                      Setting.COPY_SUFFIX;

    private const string RATING_ITEM_SQL = $"COPY \"{nameof(ArticleRatingItem)}\" " +
                                           $"(\"{nameof(ArticleRatingItem.Id)}\",\"{nameof(ArticleRatingItem.CreditId)}\",\"{nameof(ArticleRatingItem.Point)}\"" +
                                           Setting.COPY_SUFFIX;

    private const string POST0_RATING_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}/0";
    private const string POST0_RATING_ITEM_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}/0";

    public ArticleRatingMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        Directory.CreateDirectory(POST0_RATING_DIRECTORY_PATH);
        Directory.CreateDirectory(POST0_RATING_ITEM_DIRECTORY_PATH);

        var periods = PeriodHelper.GetPeriods();

        var post0Sql = string.Concat(string.Format(QUERY_RATE_SQL, ""), QUERY_RATE_SQL_DATE_CONDITION);
        //pre_forum_post_0 資料最多用日期並行處理
        Parallel.ForEach(periods,
                         period =>
                         {
                             using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                             var rateLogs = cn.Query<RateLog>(post0Sql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToArray();

                             Execute(rateLogs, 0, period);
                         });

        var postTableIds = ArticleHelper.GetPostTableIds();
        postTableIds.RemoveAt(0);

        Parallel.ForEach(postTableIds,
                         postTableId =>
                         {
                             var sql = string.Format(QUERY_RATE_SQL, $"_{postTableId}");

                             using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                             var rateLogs = cn.Query<RateLog>(sql).ToArray();

                             Execute(rateLogs, postTableId);
                         });
    }

    private void Execute(RateLog[] rateLogs, int postTableId, Period? period = null)
    {
        if (!rateLogs.Any()) return;

        var articleIdDic = RelationHelper.GetArticleIdDic(rateLogs.Select(x => x.Tid).Distinct().ToArray());

        if (!articleIdDic.Any())
            return;

        var simpleMemberDic = RelationHelper.GetSimpleMemberDic(rateLogs.Select(x => x.Uid.ToString()).Distinct().ToArray());

        var articleRatingSb = new StringBuilder();
        var articleRatingItemSb = new StringBuilder();
        var ratingIdDic = new Dictionary<(uint pid, int tid, int uid), byte>();

        foreach (var rateLog in rateLogs)
        {
            var rateCreateDate = DateTimeOffset.FromUnixTimeSeconds(rateLog.Dateline);
            var ratingId = _snowflake.Generate();
            var (memberId, memberName) = simpleMemberDic.ContainsKey(rateLog.Uid) ? simpleMemberDic[rateLog.Uid] : (0, string.Empty);

            //對不到Tid的不處理
            if (!articleIdDic.ContainsKey(rateLog.Tid))
                continue;

            if (!ratingIdDic.ContainsKey((rateLog.Pid, rateLog.Tid, rateLog.Uid)))
            {
                var articleRating = new ArticleRating
                                    {
                                        Id = ratingId,
                                        ArticleId = articleIdDic[rateLog.Tid],
                                        VisibleType = VisibleType.Public,
                                        Content = rateLog.Reason,
                                        CreationDate = rateCreateDate,
                                        CreatorId = memberId,
                                        ModificationDate = rateCreateDate,
                                        ModifierId = memberId
                                    };

                articleRatingSb.Append($"{articleRating.Id}{Setting.D}{articleRating.ArticleId}{Setting.D}{(int) articleRating.VisibleType}{Setting.D}{articleRating.Content.ToCopyText()}{Setting.D}" +
                                       $"{articleRating.CreationDate}{Setting.D}{articleRating.CreatorId}{Setting.D}{articleRating.ModificationDate}{Setting.D}{articleRating.ModifierId}{Setting.D}{articleRating.Version}\n");

                var articleRatingItem = new ArticleRatingItem()
                                        {
                                            Id = ratingId,
                                            CreditId = rateLog.Extcredits,
                                            Point = rateLog.Score,
                                            CreationDate = rateCreateDate,
                                            CreatorId = memberId,
                                            ModificationDate = rateCreateDate,
                                            ModifierId = memberId
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
                                            CreatorId = memberId,
                                            ModificationDate = rateCreateDate,
                                            ModifierId = memberId
                                        };

                articleRatingItemSb.Append($"{articleRatingItem.Id}{Setting.D}{articleRatingItem.CreditId}{Setting.D}{articleRatingItem.Point}{Setting.D}" +
                                           $"{articleRatingItem.CreationDate}{Setting.D}{articleRatingItem.CreatorId}{Setting.D}{articleRatingItem.ModificationDate}{Setting.D}{articleRatingItem.ModifierId}{Setting.D}{articleRatingItem.Version}\n");
            }
        }

        if (articleRatingSb.Length == 0) return;

        var insertRatingSql = string.Concat(RATING_SQL, articleRatingSb);
        var insertRatingItemSql = string.Concat(RATING_ITEM_SQL, articleRatingItemSb);

        var ratingPath = postTableId == 0 ? $"{POST0_RATING_DIRECTORY_PATH}/{period!.FileName}" : $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRating)}/{postTableId}.sql";
        File.WriteAllText(ratingPath, insertRatingSql);
        Console.WriteLine(ratingPath);
        
        var ratingItemPath = postTableId == 0 ? $"{POST0_RATING_ITEM_DIRECTORY_PATH}/{period!.FileName}" : $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}/{postTableId}.sql";
        File.WriteAllText(ratingItemPath, insertRatingItemSql);
        Console.WriteLine(ratingItemPath);
    }
}