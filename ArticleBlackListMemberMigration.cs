using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Lctech.Jkf.Forum.Domain.Entities;
using MySqlConnector;

namespace ForumDataMigration;

public class ArticleBlackListMemberMigration
{
    private const string BLACK_LIST_SQL = $"COPY \"{nameof(ArticleBlackListMember)}\" " +
                                          $"(\"{nameof(ArticleBlackListMember.Id)}\",\"{nameof(ArticleBlackListMember.MemberId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_BLACK_LIST_SQL = $@"SELECT tid AS Id, uid AS creatorId,blackusernames, FROM_UNIXTIME(dateline) AS CreationDate FROM pre_forum_tblacklist";
    private const string BLACK_USER_NAMES = "BlackUserNames";
    private static readonly Regex UserNameRegex = new($@"s:\d+:""(?<{BLACK_USER_NAMES}>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Dictionary<string, long> MemberNameDic = MemberHelper.GetMemberNameDic();

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        var massageBlackLists = (await cn.QueryAsync<BlackListMember>(new CommandDefinition(QUERY_BLACK_LIST_SQL, cancellationToken: cancellationToken))).ToArray();

        var blackListSb = new StringBuilder(BLACK_LIST_SQL);

        foreach (var blackList in massageBlackLists)
        {
            blackList.ModifierId = blackList.CreatorId;
            blackList.ModificationDate = blackList.CreationDate;
            var userNames = UserNameRegex.Matches(blackList.BlackUserNames).Select(x => x.Groups[BLACK_USER_NAMES].Value);

            var memberIdList = new List<long>();

            foreach (var userName in userNames)
            {
                blackList.MemberId = MemberNameDic.GetValueOrDefault(userName.Trim());

                if (blackList.MemberId == default)
                {
                    Console.WriteLine(userName + " not found!");

                    continue;
                }

                //不塞重複的資料
                if (memberIdList.Contains(blackList.MemberId))
                    continue;

                memberIdList.Add(blackList.MemberId);

                blackListSb.AppendValueLine(blackList.Id, blackList.MemberId,
                                            blackList.CreationDate, blackList.CreatorId, blackList.ModificationDate, blackList.ModifierId, blackList.Version);
            }
        }

        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(ArticleBlackListMember)}.sql", blackListSb.ToString());
    }
}