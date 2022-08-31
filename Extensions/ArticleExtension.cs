using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Netcorext.Algorithms;

namespace ForumDataMigration.Extensions;

public static class ArticleExtension
{
    public static void SetVoteItemHistory(this ICollection<ArticleVoteItem> items, IEnumerable<PollVoter> pollVoters, ISnowflake snowflake,Dictionary<long, long> memberUidDic)
    {
        foreach (var pollVoter in pollVoters)
        {
            var creationDate = DateTimeOffset.FromUnixTimeSeconds(pollVoter.Dateline);

            foreach (var item in items)
            {
                if (pollVoter.Options.Contains(item.Id.ToString()))
                    item.Histories.Add(new ArticleVoteItemHistory
                                       {
                                           Id = snowflake.Generate(),
                                           ArticleVoteItemId = item.Id,
                                           CreationDate = creationDate,
                                           CreatorId = memberUidDic.GetValueOrDefault(pollVoter.Uid),
                                           ModificationDate = creationDate,
                                           ModifierId = 0,
                                       });
            }
        }
    }
}