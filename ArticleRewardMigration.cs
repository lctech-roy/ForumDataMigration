using System.Collections.Concurrent;
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
                                              $"(\"{nameof(ArticleReward.Id)}\",\"{nameof(ArticleReward.Point)}\",\"{nameof(ArticleReward.ExpirationDate)}\"" +
                                              $",\"{nameof(ArticleReward.SolveCommentId)}\",\"{nameof(ArticleReward.SolveDate)}\",\"{nameof(ArticleReward.AllowAdminSolveDate)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_ARTICLE_PARTITION_NAME_SQL = @"SELECT child.relname FROM pg_inherits
                                                              JOIN pg_class parent ON pg_inherits.inhparent = parent.oid
                                                              JOIN pg_class child ON pg_inherits.inhrelid = child.oid
                                                              WHERE parent.relname='Article';";

    private const string QUERY_ARTICLE_PRICE_SQL = @"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
                                                     SELECT ""Id"",""Price"" AS Point,""CreationDate"",""CreatorId"" FROM ""Article"" WHERE ""Type"" = 3";

    private const string QUERY_COMMENT_SQL = @"SELECT c.""Id"" AS ArticleId, c2.""Id"" AS SolveCommentId FROM ""Comment"" c
                                               LEFT JOIN ""Comment"" c2 ON c2.""RootId"" = c.""Id"" AND c2.""CreationDate"" = c.""CreationDate"" + interval '1' second
                                               WHERE c.""Id"" = ANY(@RootIds)";

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        FileHelper.RemoveFiles(new[] { $"{ARTICLE_REWARD_PATH}.sql" });

        var rewardDic = new Dictionary<string, ArticleReward[]>();

        await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        {
            var command = new CommandDefinition(QUERY_ARTICLE_PARTITION_NAME_SQL, cancellationToken: cancellationToken);

            var partitionNames = (await cn.QueryAsync<string>(command)).ToArray();

            foreach (var partitionName in partitionNames)
            {
                rewardDic.TryAdd(partitionName, Array.Empty<ArticleReward>());
            }
        }

        await CommonHelper.WatchTimeAsync("total time",
                                          async () =>
                                          {
                                              await Parallel.ForEachAsync(rewardDic.Keys,
                                                                          CommonHelper.GetParallelOptions(cancellationToken),
                                                                          async (partitionTableName, token) =>
                                                                          {
                                                                              Console.WriteLine(partitionTableName + " start");

                                                                              await CommonHelper.WatchTimeAsync(partitionTableName, async () =>
                                                                                                                                    {
                                                                                                                                        await using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

                                                                                                                                        var command = new CommandDefinition(QUERY_ARTICLE_PRICE_SQL.Replace("Article", partitionTableName), cancellationToken: cancellationToken);

                                                                                                                                        rewardDic[partitionTableName] = (await cn.QueryAsync<ArticleReward>(command)).ToArray();
                                                                                                                                    }
                                                                                                               );
                                                                          });
                                          });

        var solvedRewardDic = new ConcurrentDictionary<long, long>();

        await CommonHelper.WatchTimeAsync("total time",
                                          async () =>
                                          {
                                              await Parallel.ForEachAsync(rewardDic.Keys,
                                                                          CommonHelper.GetParallelOptions(cancellationToken),
                                                                          async (partitionTableName, token) =>
                                                                          {
                                                                              Console.WriteLine(partitionTableName + " start");

                                                                              await CommonHelper.WatchTimeAsync(partitionTableName,
                                                                                                                async () =>
                                                                                                                {
                                                                                                                    await using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

                                                                                                                    var solvedRewardIds = rewardDic[partitionTableName].Where(x => x.Point < 0).Select(x => x.Id).ToArray();

                                                                                                                    var command = new CommandDefinition(QUERY_COMMENT_SQL, new { RootIds = solvedRewardIds }, cancellationToken: cancellationToken);

                                                                                                                    var newSolvedRewards = await cn.QueryAsync<(long articleId, long solveCommentId)>(command);

                                                                                                                    foreach (var newSolvedReward in newSolvedRewards)
                                                                                                                    {
                                                                                                                        solvedRewardDic.TryAdd(newSolvedReward.articleId, newSolvedReward.solveCommentId);
                                                                                                                    }
                                                                                                                }
                                                                                                               );
                                                                          });
                                          });

        var rewardSb = new StringBuilder();

        foreach (var reward in rewardDic.SelectMany(rewardPair => rewardPair.Value))
        {
            reward.AllowAdminSolveDate = reward.CreationDate.AddDays(1);
            reward.ExpirationDate = reward.CreationDate.AddDays(CommonSetting.RewardExpirationDay);
            reward.ModificationDate = reward.CreationDate;
            reward.ModifierId = reward.CreatorId;

            //已解決
            if (reward.Point < 0)
            {
                reward.SolveCommentId = solvedRewardDic.GetValueOrDefault(reward.Id);
                reward.SolveDate = reward.CreationDate.AddSeconds(1);
            }

            reward.Point = Math.Abs(reward.Point);

            rewardSb.AppendValueLine(reward.Id, reward.Point, reward.ExpirationDate,
                                     reward.SolveCommentId.ToCopyValue(), reward.SolveDate.ToCopyValue(), reward.AllowAdminSolveDate,
                                     reward.CreationDate, reward.CreatorId, reward.ModificationDate, reward.ModifierId, reward.Version);
        }

        File.WriteAllText($"{ARTICLE_REWARD_PATH}.sql", string.Concat(COPY_REWARD_PREFIX, rewardSb.ToString()));
    }
}