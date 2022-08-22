using System.Text;
using Netcorext.Algorithms;
using Dapper;
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

    private const string QUERY_RATE_SQL = @"SELECT thread.fid,thread.typeid, rate.* FROM pre_forum_post{0} AS post 
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
    private const string POST0_RATING_JASON_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingJson)}/0";
    private const string POST0_RATING_ITEM_DIRECTORY_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}/0";

    private static readonly Dictionary<int, long> BoardDic = RelationHelper.GetBoardDic();
    private static readonly Dictionary<int, long> CategoryDic = RelationHelper.GetCategoryDic();

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

        var post0Sql = string.Concat(string.Format(QUERY_RATE_SQL, ""), QUERY_RATE_SQL_DATE_CONDITION);

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
                                                                         var sql = string.Format(QUERY_RATE_SQL, $"_{postTableId}");

                                                                         await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

                                                                         var rateLogs = cn.Query<RateLog>(sql).ToArray();

                                                                         await ExecuteAsync(rateLogs, postTableId, cancellationToken: cancellationToken);
                                                                     });
    }
    private async Task ExecuteAsync(RateLog[] rateLogs, int postTableId, Period? period = null, CancellationToken cancellationToken = default)
    {
        if (!rateLogs.Any()) return;

        var articleIdDic = await RelationHelper.GetArticleIdDicAsync(rateLogs.Select(x => x.Tid).Distinct().ToArray(),cancellationToken);

        if (!articleIdDic.Any())
            return;

        var simpleMemberDic = await RelationHelper.GetSimpleMemberDicAsync(rateLogs.Select(x => x.Uid.ToString()).Distinct().ToArray(), cancellationToken);

        var articleRatingSb = new StringBuilder();
        var articleRatingJsonSb = new StringBuilder();
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
                var articleId = articleIdDic[rateLog.Tid];
                
                var articleRating = new ArticleRatingJson()
                                    {
                                        Id = ratingId,
                                        ArticleId = articleId,
                                        VisibleType = VisibleType.Public,
                                        Content = rateLog.Reason,
                                        CreationDate = rateCreateDate,
                                        CreatorId = memberId,
                                        ModificationDate = rateCreateDate,
                                        ModifierId = memberId,

                                        //for search json
                                        RootId = articleId,
                                        BoardId = BoardDic.ContainsKey(rateLog.Fid) ? BoardDic[rateLog.Fid] : 0,
                                        CategoryId = CategoryDic.ContainsKey(rateLog.Typeid) ? CategoryDic[rateLog.Typeid] : 0,
                                        CreatorUid = rateLog.Uid,
                                        CreatorName = memberName,
                                        ModifierUid = rateLog.Uid,
                                        ModifierName = memberName
                                    };

                articleRatingSb.Append($"{articleRating.Id}{Setting.D}{articleRating.ArticleId}{Setting.D}{(int) articleRating.VisibleType}{Setting.D}{articleRating.Content.ToCopyText()}{Setting.D}" +
                                       $"{articleRating.CreationDate}{Setting.D}{articleRating.CreatorId}{Setting.D}{articleRating.ModificationDate}{Setting.D}{articleRating.ModifierId}{Setting.D}{articleRating.Version}\n");

                var json = await JsonHelper.GetJsonAsync(articleRating, cancellationToken);
                articleRatingJsonSb.Append($"{json}\n");
                
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
        await File.WriteAllTextAsync(ratingPath, insertRatingSql, cancellationToken);
        Console.WriteLine(ratingPath);
        
        var ratingJsonPath = postTableId == 0 ? $"{POST0_RATING_JASON_DIRECTORY_PATH}/{period!.FileName}" : $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingJson)}/{postTableId}.sql";
        await File.WriteAllTextAsync(ratingJsonPath, articleRatingJsonSb.ToString(), cancellationToken);
        Console.WriteLine(ratingJsonPath);

        var ratingItemPath = postTableId == 0 ? $"{POST0_RATING_ITEM_DIRECTORY_PATH}/{period!.FileName}" : $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleRatingItem)}/{postTableId}.sql";
        await File.WriteAllTextAsync(ratingItemPath, insertRatingItemSql, cancellationToken);
        Console.WriteLine(ratingItemPath);
    }
}