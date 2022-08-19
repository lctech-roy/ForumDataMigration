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
    private readonly ISnowflake _snowflake;

    public ArticleVoteMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        const string articleVoteSql = $"COPY \"{nameof(ArticleVote)}\" " +
                                      $"(\"{nameof(ArticleVote.Id)}\",\"{nameof(ArticleVote.LimitNumberOfVotes)}\",\"{nameof(ArticleVote.Voters)}\",\"{nameof(ArticleVote.PublicResult)}\"" +
                                      $",\"{nameof(ArticleVote.PublicVoter)}\",\"{nameof(ArticleVote.Deadline)}\"" + Setting.COPY_ENTITY_SUFFIX;

        const string articleVoteItemSql = $"COPY \"{nameof(ArticleVoteItem)}\" " +
                                          $"(\"{nameof(ArticleVoteItem.Id)}\",\"{nameof(ArticleVoteItem.ArticleVoteId)}\",\"{nameof(ArticleVoteItem.Name)}\",\"{nameof(ArticleVoteItem.Votes)}\"" + Setting.COPY_ENTITY_SUFFIX;

        const string articleVoteItemHistorySql = $"COPY \"{nameof(ArticleVoteItemHistory)}\" " +
                                                 $"(\"{nameof(ArticleVoteItemHistory.Id)}\",\"{nameof(ArticleVoteItemHistory.ArticleVoteItemId)}\"" + Setting.COPY_ENTITY_SUFFIX;
        
        const string queryPollSql = $@"SELECT thread.dateline,
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

        #region 轉檔前準備相關資料

        var periods = PeriodHelper.GetPeriods();
        var articleDic = RelationContainer.ArticleIdDic;
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
                                          var dynamicPolls = cn.Query<dynamic>(queryPollSql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToList();
                                          polls = Slapper.AutoMapper.MapDynamic<Poll>(dynamicPolls, false).ToList();
                                      }

                                      if (!polls.Any())
                                          return;

                                      var articleVoteSb = new StringBuilder();
                                      var articleVoteItemSb = new StringBuilder();
                                      var articleVoteItemHistorySb = new StringBuilder();

                                      foreach (var poll in polls.Where(poll => poll.Tid != 0 && articleDic.ContainsKey(poll.Tid)))
                                      {
                                          var articleId = articleDic[poll.Tid];

                                          var memberId = memberUidDic.ContainsKey(Convert.ToInt32(poll.Authorid)) ? memberUidDic[Convert.ToInt32(poll.Authorid)] : 0;

                                          var createDate = DateTimeOffset.FromUnixTimeSeconds(poll.Dateline);

                                          var articleVote = new ArticleVote
                                                            {
                                                                Id = articleId,
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
                                                                                                         ArticleVoteId = articleId,
                                                                                                         Name = y.Polloption ?? string.Empty,
                                                                                                         Votes = y.Votes,
                                                                                                         CreationDate = createDate,
                                                                                                         CreatorId = memberId,
                                                                                                         ModificationDate = createDate,
                                                                                                     }).ToArray()
                                                            };

                                          articleVote.Items.SetVoteItemHistory(poll.PollVoters, _snowflake, memberUidDic);

                                          articleVoteSb.Append($"{articleVote.Id}{Setting.D}{articleVote.LimitNumberOfVotes}{Setting.D}{articleVote.Voters}{Setting.D}{articleVote.PublicResult}{Setting.D}{articleVote.PublicVoter}{Setting.D}{articleVote.Deadline}{Setting.D}" +
                                                               $"{articleVote.CreationDate}{Setting.D}{articleVote.CreatorId}{Setting.D}{articleVote.ModificationDate}{Setting.D}{articleVote.ModifierId}{Setting.D}{articleVote.Version}\n");

                                          foreach (var articleVoteItem in articleVote.Items)
                                          {
                                              articleVoteItemSb.Append($"{articleVoteItem.Id}{Setting.D}{articleVoteItem.ArticleVoteId}{Setting.D}{articleVoteItem.Name}{Setting.D}{articleVoteItem.Votes}{Setting.D}" +
                                                                       $"{articleVoteItem.CreationDate}{Setting.D}{articleVoteItem.CreatorId}{Setting.D}{articleVoteItem.ModificationDate}{Setting.D}{articleVoteItem.ModifierId}{Setting.D}{articleVoteItem.Version}\n");

                                              foreach (var articleVoteItemHistory in articleVoteItem.Histories)
                                              {
                                                  articleVoteItemHistorySb.Append($"{articleVoteItemHistory.Id}{Setting.D}{articleVoteItemHistory.ArticleVoteItemId}{Setting.D}" +
                                                                                  $"{articleVoteItemHistory.CreationDate}{Setting.D}{articleVoteItemHistory.CreatorId}{Setting.D}{articleVoteItemHistory.ModificationDate}{Setting.D}{articleVoteItemHistory.ModifierId}{Setting.D}{articleVoteItemHistory.Version}\n");
                                              }
                                          }
                                      }

                                      if (articleVoteSb.Length > 0)
                                      {
                                          var fullPath = $"{articleVotePath}/{period.FileName}";
                                          File.WriteAllText(fullPath, string.Concat(articleVoteSql, articleVoteSb.ToString()));
                                          Console.WriteLine(fullPath);
                                      }

                                      if (articleVoteItemSb.Length > 0)
                                      {
                                          var fullPath = $"{articleVoteItemPath}/{period.FileName}";
                                          File.WriteAllText(fullPath, string.Concat(articleVoteItemSql, articleVoteItemSb.ToString()));
                                          Console.WriteLine(fullPath);
                                      }

                                      if (articleVoteItemHistorySb.Length > 0)
                                      {
                                          var fullPath = $"{articleVoteItemHistoryPath}/{period.FileName}";
                                          File.WriteAllText(fullPath, string.Concat(articleVoteItemHistorySql, articleVoteSb.ToString()));
                                          Console.WriteLine(fullPath);
                                      }
                                  });
    }
}