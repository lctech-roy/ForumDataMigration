using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Domain.Entities;
using Npgsql;

namespace ForumDataMigration;

public class ArticleRewardMigration
{
    private static readonly CommonSetting CommonSetting = ArticleHelper.GetCommonSetting();

    private const string ARTICLE_REWARD_PATH = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}";

    private const string COPY_REWARD_PREFIX = $"COPY \"{nameof(ArticleReward)}\" " +
                                              $"(\"{nameof(ArticleReward.Id)}\",\"{nameof(ArticleReward.Point)}\",\"{nameof(ArticleReward.IsExpired)}\",\"{nameof(ArticleReward.ExpirationDate)}\"" +
                                              $",\"{nameof(ArticleReward.SolveCommentId)}\",\"{nameof(ArticleReward.SolveDate)}\",\"{nameof(ArticleReward.AllowAdminSolveDate)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_ARTICLE_PRICE_SQL = @"SELECT ""Id"",""Price"" AS Point,""CreationDate"",""CreatorId"" FROM ""Article"" WHERE ""Type"" = 3";

    private const string QUERY_COMMENT_SQL = @"SELECT c.""Id"" AS ArticleId, c2.""Id"" AS SolveCommentId FROM ""Comment"" c
                                               LEFT JOIN ""Comment"" c2 ON c2.""RootId"" = c.""Id"" AND c2.""CreationDate"" = c.""CreationDate"" + interval '1' second
                                               WHERE c.""Id"" = ANY(@RootIds)";

    public static async Task MigrationAsync(CancellationToken cancellationToken)
    {
        FileHelper.RemoveFiles(new[] { $"{ARTICLE_REWARD_PATH}.sql" });

        ArticleReward[] articleRewards = default!;

        await CommonHelper.WatchTimeAsync(nameof(QUERY_ARTICLE_PRICE_SQL),
                                          async () =>
                                          {
                                              await using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

                                              var command = new CommandDefinition(QUERY_ARTICLE_PRICE_SQL, cancellationToken: cancellationToken);

                                              articleRewards = (await cn.QueryAsync<ArticleReward>(command)).ToArray();
                                          });

        var solvedRewardDic = new Dictionary<long, long>();

        await CommonHelper.WatchTimeAsync(nameof(QUERY_COMMENT_SQL),
                                          async () =>
                                          {
                                              await using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

                                              var solvedRewardIds = articleRewards.Where(x => x.Point < 0).Select(x => x.Id).ToArray();

                                              var command = new CommandDefinition(QUERY_COMMENT_SQL, new { RootIds = solvedRewardIds }, cancellationToken: cancellationToken);

                                              var newSolvedRewards = await cn.QueryAsync<(long articleId, long solveCommentId)>(command);

                                              foreach (var newSolvedReward in newSolvedRewards)
                                              {
                                                  solvedRewardDic.TryAdd(newSolvedReward.articleId, newSolvedReward.solveCommentId);
                                              }
                                          });

        var rewardSb = new StringBuilder();

        foreach (var reward in articleRewards)
        {
            reward.AllowAdminSolveDate = reward.CreationDate.AddDays(1);
            reward.ExpirationDate = reward.CreationDate.AddDays(CommonSetting.RewardExpirationDay);
            reward.ModificationDate = reward.CreationDate;
            reward.ModifierId = reward.CreatorId;

            //已解決
            if (reward.Point < 0)
            {
                var solveCommentId = solvedRewardDic.GetValueOrDefault(reward.Id);
                
                //髒資料,有些是文章已被刪除
                if (solveCommentId == default)
                {
                    reward.IsExpired = true;
                }
                else
                {
                    reward.SolveCommentId = solveCommentId;
                    reward.SolveDate = reward.CreationDate.AddSeconds(1);
                    reward.IsExpired = false;
                }
            }
            else
            {
                reward.IsExpired = DateTimeOffset.UtcNow >= reward.ExpirationDate;
            }

            reward.Point = Math.Abs(reward.Point);

            rewardSb.AppendValueLine(reward.Id, reward.Point, reward.IsExpired, reward.ExpirationDate,
                                     reward.SolveCommentId.ToCopyValue(), reward.SolveDate.ToCopyValue(), reward.AllowAdminSolveDate,
                                     reward.CreationDate, reward.CreatorId, reward.ModificationDate, reward.ModifierId, reward.Version);
        }

        await File.WriteAllTextAsync($"{ARTICLE_REWARD_PATH}.sql", string.Concat(COPY_REWARD_PREFIX, rewardSb.ToString()), cancellationToken);
    }
}