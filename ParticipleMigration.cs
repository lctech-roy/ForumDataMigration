using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;
using Lctech.Participle.Domain.Entities;
using Lctech.Participle.Enums;
using MySqlConnector;

namespace ForumDataMigration;

public class ParticipleMigration
{
    private const string PARTICLE_SQL = $"COPY \"{nameof(SensitiveWordFilter)}\" " +
                                        $"(\"{nameof(SensitiveWordFilter.Id)}\",\"{nameof(SensitiveWordFilter.Action)}\",\"{nameof(SensitiveWordFilter.ReplaceWord)}\"" +
                                        $",\"{nameof(SensitiveWordFilter.Pattern)}\",\"{nameof(SensitiveWordFilter.Tags)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_WORD_SQL = $@"SELECT id,find,replacement FROM pre_common_word";

    private const string TAG_NAME = "common";
    private const string NUMBER_GROUP = "number";
    private const string NUMBER_PATTERN = $@"\{{(?<{NUMBER_GROUP}>\d+)\}}";
    private static readonly Regex NumberRegex = new(NUMBER_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task MigrationAsync(CancellationToken cancellationToken)
    {
        await using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        var commonWords = (await cn.QueryAsync<CommonWord>(new CommandDefinition(QUERY_WORD_SQL, cancellationToken: cancellationToken))).ToArray();

        var commonWordSb = new StringBuilder(PARTICLE_SQL);

        foreach (var commonWord in commonWords)
        {
            var sensitiveWordFilter = new SensitiveWordFilter
                                      {
                                          Id = commonWord.Id,
                                          Tags = TAG_NAME,
                                          Action = commonWord.Replacement switch
                                                   {
                                                       "{MOD}" => WordFilterActionType.Silent,
                                                       "{BANNED}" => WordFilterActionType.Warning,
                                                       _ => WordFilterActionType.Replace
                                                   }
                                      };

            sensitiveWordFilter.ReplaceWord = sensitiveWordFilter.Action == WordFilterActionType.Replace ? commonWord.Replacement : null;

            var pattern = NumberRegex.Replace(commonWord.Find, m =>
                                                               {
                                                                   if (!string.IsNullOrEmpty(m.Groups[NUMBER_GROUP].Value))
                                                                       return $".{{0,{m.Groups[NUMBER_GROUP].Value}}}";

                                                                   return commonWord.Find;
                                                               });

            sensitiveWordFilter.Pattern = pattern;
            sensitiveWordFilter.CreationDate = DateTimeOffset.UtcNow;
            sensitiveWordFilter.ModificationDate = DateTimeOffset.UtcNow;
            sensitiveWordFilter.CreatorId = 1;
            sensitiveWordFilter.ModifierId = 1;
            
            commonWordSb.AppendValueLine(sensitiveWordFilter.Id, (int)sensitiveWordFilter.Action, sensitiveWordFilter.ReplaceWord.ToCopyText(),sensitiveWordFilter.Pattern.ToCopyText(),sensitiveWordFilter.Tags,
                                         sensitiveWordFilter.CreationDate, sensitiveWordFilter.CreatorId, sensitiveWordFilter.ModificationDate, sensitiveWordFilter.ModifierId, sensitiveWordFilter.Version);
        }
        
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(SensitiveWordFilter)}.sql", commonWordSb.ToString());
    }
}