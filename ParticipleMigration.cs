using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;
using Lctech.Participle.Domain.Entities;
using Lctech.Participle.Enums;
using MySqlConnector;
using Netcorext.Algorithms;

namespace ForumDataMigration;

public class ParticipleMigration
{
    private readonly ISnowflake _snowflake;

    private const string PARTICLE_SQL = $"COPY \"{nameof(SensitiveWordFilter)}\" " +
                                        $"(\"{nameof(SensitiveWordFilter.Id)}\",\"{nameof(SensitiveWordFilter.Action)}\",\"{nameof(SensitiveWordFilter.ReplaceWord)}\"" +
                                        $",\"{nameof(SensitiveWordFilter.Pattern)}\",\"{nameof(SensitiveWordFilter.Tags)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_WORD_SQL = $@"SELECT id,find,replacement FROM pre_common_word";

    private const string TAG_NAME = "common";
    private const string TAG_NAME_MASSAGE = "1128";
    private const string NUMBER_GROUP = "number";
    private const string NUMBER_PATTERN = $@"\{{(?<{NUMBER_GROUP}>\d+)\}}";
    private static readonly Regex NumberRegex = new(NUMBER_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ParticipleMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

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

            sensitiveWordFilter.Pattern = pattern.Replace("?", "\\?").Replace(".", "\\.");
            sensitiveWordFilter.CreationDate = DateTimeOffset.UtcNow;
            sensitiveWordFilter.ModificationDate = DateTimeOffset.UtcNow;
            sensitiveWordFilter.CreatorId = 1;
            sensitiveWordFilter.ModifierId = 1;

            commonWordSb.AppendValueLine(sensitiveWordFilter.Id, (int) sensitiveWordFilter.Action, sensitiveWordFilter.ReplaceWord.ToCopyText(), sensitiveWordFilter.Pattern.ToCopyText(), sensitiveWordFilter.Tags,
                                         sensitiveWordFilter.CreationDate, sensitiveWordFilter.CreatorId, sensitiveWordFilter.ModificationDate, sensitiveWordFilter.ModifierId, sensitiveWordFilter.Version);
        }

        var massageSilentWords = new List<string>
                                 {
                                     "全套|半套|車燈|車頭燈|1S|2S|NS|MMT|罩杯|BJ|激情|SEX|江西|無套|帶套|卸甲|茶資|茶杯|茶溫|茶齡|吃魚|喝茶|魚訊|茶訊|外送|排毒|愛愛|做愛|慾罷不能|巨乳|挑逗|大鵰",
                                     "手排|手愛|約愛|口交|大奶|淫蕩|噴發|飢渴|摩擦|高潮|性愛|護雕|誘惑|胸器|貼身|攝護腺|胸奴|LG|好色|小穴|欲仙欲死|雙飛|毒龍|清溝|口爆|泰洗|泰國洗|泰國浴|泰浴|騷|加碼|加購",
                                     "喇舌|出水|試車|救火|滅火|品嚐|冰火|後宮|黑森林|調教|狂野|消火|色情|情色|上空|回沖|全壘打|吹簫|深入|半身|血脈噴張|浪潮不斷|對對碰|撫摸|生理需求|深喉嚨|快感|浪叫|情趣|騷|殘廢澡",
                                     "誘人|波濤洶湧|慾|私處|私密處|鹹濕|挑逗|下流|茶|吸|吮|性福|小頭|升級|可升|升等|可UP|內診|加值|全半|1|攻頂|無限次|N次|尺度|絕頂|升天|吃到飽|攻山頂|蛋蛋|進階|拆|全車|加量|跳級",
                                     "禽獸|G點|殘廢|直達天堂|全餐|昇天|排除毒素|玩到底|爽|有口皆碑|親密|吸吮|陰陽|直挺|內側|欲|洩|到底|頂級|把玩|需求|豪峰|新車|撫慰|無限|激發|宣洩|不限|觸感|口味|寵愛|下面|所需",
                                     "濕|窄|下海|車庫|邪惡|貪念|探索|玩法|學生|包到好|臨幸|傳播|18禁|未滿18歲|選妃|活塞|引爆|回春|持久|有口皆碑|滑體|口碑|深入|插入|進入|私人魚|舔|魚水之歡|笙歌|頂天|一柱|握|套|全軀",
                                     "火車便當|疊疊樂|空虛|弟弟|老二|深度|酥麻|含苞待放|鮑|吹|幫你做|零距離|炒飯|吹奏|演奏|槍|上膛|深處|輕撫|伴遊|貨色|床聲|擁抱|杯|凶|胸器|后宮|選秀|極樂|銷魂|出租|舌|快活|樓鳳",
                                     "品嘗|租車|伸縮|操控|愛人|雄風|騎|奶控|桑拿|肉體|獸性|拈花惹草|翹臀|撫弄|蛇|腫脹|採補|飛天|雙響泡|炮|砲|看日出|山頂|真奶|無膜|獸性|上訴|重鹹|弄|妹頭|前殘|後殘|高潮|疼愛|享樂",
                                     "手天使|肉棒|龍穴|壯陽|網紅|網美|女郎|小模|模特兒|哺乳|奶粉|單親|淫|妓|telegram|駱駝蹄|上岸|叫床|床叫|攝護腺|莖|口爆|噴水|香腸|摳|家暴|欠債|姦|尻|賭博|酗酒|打人|攝護腺"
                                 };

        foreach (var sensitiveWordFilter in massageSilentWords.Select(silentWord => new SensitiveWordFilter
                                                                                    {
                                                                                        Id = _snowflake.Generate(),
                                                                                        Tags = TAG_NAME_MASSAGE,
                                                                                        Action = WordFilterActionType.Warning,
                                                                                        Pattern = silentWord.Replace("?", "\\?").Replace(".", "\\."),
                                                                                        CreationDate = DateTimeOffset.UtcNow,
                                                                                        ModificationDate = DateTimeOffset.UtcNow,
                                                                                        CreatorId = 1,
                                                                                        ModifierId = 1
                                                                                    }))
        {
            commonWordSb.AppendValueLine(sensitiveWordFilter.Id, (int) sensitiveWordFilter.Action, sensitiveWordFilter.ReplaceWord.ToCopyText(), sensitiveWordFilter.Pattern.ToCopyText(), sensitiveWordFilter.Tags,
                                         sensitiveWordFilter.CreationDate, sensitiveWordFilter.CreatorId, sensitiveWordFilter.ModificationDate, sensitiveWordFilter.ModifierId, sensitiveWordFilter.Version);
        }

        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(SensitiveWordFilter)}.sql", commonWordSb.ToString());
    }
}