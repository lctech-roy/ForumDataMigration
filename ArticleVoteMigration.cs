using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using MySqlConnector;
using Netcorext.Algorithms;

namespace ForumDataMigration;

public class ArticleVoteMigration
{
    private const string VOTE_SQL = $"COPY \"{nameof(ArticleVote)}\" " +
                                    $"(\"{nameof(ArticleVote.Id)}\",\"{nameof(ArticleVote.LimitNumberOfVotes)}\",\"{nameof(ArticleVote.Voters)}\",\"{nameof(ArticleVote.PublicResult)}\"" +
                                    $",\"{nameof(ArticleVote.PublicVoter)}\",\"{nameof(ArticleVote.Deadline)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string VOTE_ITEM_SQL = $"COPY \"{nameof(ArticleVoteItem)}\" " +
                                         $"(\"{nameof(ArticleVoteItem.Id)}\",\"{nameof(ArticleVoteItem.ArticleVoteId)}\",\"{nameof(ArticleVoteItem.Name)}\",\"{nameof(ArticleVoteItem.Votes)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string VOTE_ITEM_HISTORY_SQL = $"COPY \"{nameof(ArticleVoteItemHistory)}\" " +
                                                 $"(\"{nameof(ArticleVoteItemHistory.Id)}\",\"{nameof(ArticleVoteItemHistory.ArticleVoteItemId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_POLL_SQL = $@"SELECT thread.dateline,
                                                thread.authorid,
                                                poll.tid  AS Tid,
                                                poll.voters AS voters,
                                                poll.maxchoices AS maxchoices,
                                                poll.overt AS overt,
                                                poll.visible AS visible,
                                                poll.expiration AS expiration,
                                                pollOption.tid AS {nameof(Poll.PollOptions)}_Tid,
                                                pollOption.polloptionid AS {nameof(Poll.PollOptions)}_polloptionid,
                                                pollOption.votes AS {nameof(Poll.PollOptions)}_votes,
                                                pollOption.polloption AS {nameof(Poll.PollOptions)}_polloption,
                                                pollVoter.tid AS {nameof(Poll.PollVoters)}_Tid,
                                                pollVoter.uid AS {nameof(Poll.PollVoters)}_uid,
                                                pollVoter.options AS {nameof(Poll.PollVoters)}_options,
                                                pollVoter.dateline AS {nameof(Poll.PollVoters)}_dateline
                                                FROM (
                                                    SELECT tid,dateline,authorid FROM `pre_forum_thread`
                                                    where special=1 AND dateline >= @Start AND dateline < @End
                                                ) AS thread
                                                LEFT JOIN pre_forum_poll as poll on thread.tid = poll.tid
                                                LEFT JOIN pre_forum_polloption as pollOption on poll.tid = pollOption.tid
                                                LEFT JOIN pre_forum_pollvoter as pollVoter on poll.tid = pollVoter.tid";

    private readonly ISnowflake _snowflake;

    public ArticleVoteMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        #region 轉檔前準備相關資料

        var periods = PeriodHelper.GetPeriods();
        var dic = RelationContainer.ArticleIdDic;
        var memberUidDic = RelationHelper.GetMemberUidDic();

        #endregion

        const string articleVotePath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVote)}";
        Directory.CreateDirectory(articleVotePath);

        const string articleVoteItemPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItem)}";
        Directory.CreateDirectory(articleVoteItemPath);

        const string articleVoteItemHistoryPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleVoteItemHistory)}";
        Directory.CreateDirectory(articleVoteItemHistoryPath);

        Parallel.ForEach(periods, period =>
                                  {
                                      List<Poll> polls;

                                      using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION))
                                      {
                                          var dynamicPolls = cn.Query<dynamic>(QUERY_POLL_SQL, new { Start = period.StartSeconds, End = period.EndSeconds }).ToList();
                                          polls = Slapper.AutoMapper.MapDynamic<Poll>(dynamicPolls, false).ToList();
                                      }

                                      if (!polls.Any())
                                          return;

                                      var voteSb = new StringBuilder();
                                      var voteItemSb = new StringBuilder();
                                      var voteItemHistorySb = new StringBuilder();

                                      foreach (var poll in polls.Where(poll => poll.Tid != 0 && dic.ContainsKey(poll.Tid)))
                                      {
                                          var id = dic[poll.Tid];

                                          var memberId = memberUidDic.ContainsKey(Convert.ToInt32(poll.Authorid)) ? memberUidDic[Convert.ToInt32(poll.Authorid)] : 0;

                                          var createDate = DateTimeOffset.FromUnixTimeSeconds(poll.Dateline);

                                          var vote = new ArticleVote
                                                     {
                                                         Id = id,
                                                         LimitNumberOfVotes = poll.Maxchoices,
                                                         Voters = poll.Voters,
                                                         PublicResult = poll.Visible,
                                                         PublicVoter = poll.Overt,
                                                         Deadline = DateTimeOffset.FromUnixTimeSeconds(poll.Expiration),
                                                         CreationDate = createDate,
                                                         CreatorId = memberId,
                                                         ModificationDate = createDate,
                                                         ModifierId = memberId,
                                                         Items = poll.PollOptions.Select(y => new ArticleVoteItem
                                                                                              {
                                                                                                  Id = y.Polloptionid,
                                                                                                  ArticleVoteId = id,
                                                                                                  Name = y.Polloption ?? string.Empty,
                                                                                                  Votes = y.Votes,
                                                                                                  CreationDate = createDate,
                                                                                                  CreatorId = memberId,
                                                                                                  ModificationDate = createDate,
                                                                                              }).ToArray()
                                                     };

                                          vote.Items.SetVoteItemHistory(poll.PollVoters, _snowflake, memberUidDic);
                                          
                                          voteSb.AppendValueLine(vote.Id, vote.LimitNumberOfVotes, vote.Voters, vote.PublicResult, vote.PublicVoter, vote.Deadline,
                                                                  vote.CreationDate, vote.CreatorId, vote.ModificationDate, vote.ModifierId, vote.Version);

                                          foreach (var voteItem in vote.Items)
                                          {
                                              voteItemSb.AppendValueLine(voteItem.Id,voteItem.ArticleVoteId,voteItem.Name,voteItem.Votes,
                                                                          voteItem.CreationDate,voteItem.CreatorId,voteItem.ModificationDate,voteItem.ModifierId,voteItem.Version);

                                              foreach (var voteItemHistory in voteItem.Histories)
                                              {
                                                  voteItemHistorySb.AppendValueLine(voteItemHistory.Id,voteItemHistory.ArticleVoteItemId,
                                                                                    voteItemHistory.CreationDate,voteItemHistory.CreatorId,voteItemHistory.ModificationDate,voteItemHistory.ModifierId,voteItemHistory.Version);
                                              }
                                          }
                                      }

                                      if (voteSb.Length > 0)
                                      {
                                          var fullPath = $"{articleVotePath}/{period.FileName}";
                                          File.WriteAllText(fullPath, string.Concat(VOTE_SQL, voteSb.ToString()));
                                          Console.WriteLine(fullPath);
                                      }

                                      if (voteItemSb.Length > 0)
                                      {
                                          var fullPath = $"{articleVoteItemPath}/{period.FileName}";
                                          File.WriteAllText(fullPath, string.Concat(VOTE_ITEM_SQL, voteItemSb.ToString()));
                                          Console.WriteLine(fullPath);
                                      }

                                      if (voteItemHistorySb.Length > 0)
                                      {
                                          var fullPath = $"{articleVoteItemHistoryPath}/{period.FileName}";
                                          File.WriteAllText(fullPath, string.Concat(VOTE_ITEM_HISTORY_SQL, voteSb.ToString()));
                                          Console.WriteLine(fullPath);
                                      }
                                  });
    }
}