using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Npgsql;
using Polly;

namespace ForumDataMigration;

public class RewardMigration
{
    private static readonly CommonSetting CommonSetting = ArticleHelper.GetCommonSetting();
    
    private const string COPY_REWARD_PREFIX = $"COPY \"{nameof(ArticleReward)}\" " +
                                              $"(\"{nameof(ArticleReward.Id)}\",\"{nameof(ArticleReward.Point)}\",\"{nameof(ArticleReward.ExpirationDate)}\"" +
                                              $",\"{nameof(ArticleReward.SolveCommentId)}\",\"{nameof(ArticleReward.SolveDate)}\",\"{nameof(ArticleReward.AllowAdminSolveDate)}\"" + Setting.COPY_ENTITY_SUFFIX;
    
    private const string QUERY_ARTICLE_SQL = @"SELECT ""Id"",""Price"" AS Point,""CreationDate"",""CreatorId"" FROM ""Article"" WHERE ""Type"" = 3";

    private const string QUERY_COMMENT_SQL = @"SELECT c.""Id"" AS ArticleId,c2.""Id"" AS SolveCommentId FROM ""Comment"" AS c
                                               INNER JOIN ""Comment"" c2 ON c.""RootId"" = c2.""RootId"" AND c2.""CreationDate"" = c.""CreationDate"" + interval '1' second
                                               WHERE c.""RootId"" IN (@RootIds) AND c.""Level"" =1";
    //236566694062225
    //236566698144381
    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        #region 轉檔前準備相關資料

        var memberUidDic = RelationHelper.GetMemberUidDic();
        
        #endregion
        
        var rewards = Array.Empty<ArticleReward>();

        await using (var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION))
        {
            var command = new CommandDefinition(QUERY_ARTICLE_SQL, cancellationToken: cancellationToken);

            rewards = (await cn.QueryAsync<ArticleReward>(command)).ToArray();
        }

        if (!rewards.Any())
            return;

        var rewardIds = rewards.Where(x => x.Point < 0).Select(x=>x.Id);

        var solvedRewardDic = new Dictionary<long, long>();

        await using (var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION))
        {
            var command = new CommandDefinition(QUERY_COMMENT_SQL, new { RootIds = rewardIds }, cancellationToken: cancellationToken);
            
            solvedRewardDic = (await cn.QueryAsync<(long articleId,long solveCommentId)>(command)).ToDictionary(t => t.articleId, t => t.solveCommentId);;
        }

        var rewardSb = new StringBuilder();
        
        foreach (var reward in rewards)
        {
            reward.AllowAdminSolveDate = reward.CreationDate.AddDays(1);
            reward.ExpirationDate = reward.CreationDate.AddDays(CommonSetting.RewardExpirationDay);
            reward.ModificationDate = reward.CreationDate;
            reward.ModifierId = reward.CreatorId;
            
            //已解決
            if (reward.Point >= 0) continue;

            reward.Point = Math.Abs(reward.Point);
            reward.SolveCommentId = solvedRewardDic.GetValueOrDefault(reward.Id);
            reward.SolveDate = reward.CreationDate.AddSeconds(1);
            
            rewardSb.AppendValueLine(reward.Id, reward.Point, reward.ExpirationDate,
                                     reward.SolveCommentId.ToCopyValue(), reward.SolveDate.ToCopyValue(), reward.AllowAdminSolveDate,
                                     reward.CreationDate, reward.CreatorId, reward.ModificationDate, reward.ModifierId, reward.Version);
        }
        
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleReward)}.sql", string.Concat(COPY_REWARD_PREFIX, rewardSb.ToString()));
    }
}