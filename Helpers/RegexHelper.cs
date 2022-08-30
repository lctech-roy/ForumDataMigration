using System.Diagnostics;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Models;

namespace ForumDataMigration.Helpers;

public static class RegexHelper
{
    private const string EMBED = "embed";
    private static Dictionary<string, Func<Match, int, Dictionary<int, string>, string>> BbcodeDic { get; set; }
    private static string Pattern { get; }
    private static Regex Regex { get; }

    private static readonly IEnumerable<Regex> RegexTrims = new[]
                                                            {
                                                                new Regex(@"<[^>]+>|\[[^\]]+\]", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
                                                                new Regex(@"[（）【】「」『』《》：；！？，。｜、～·﹍——]+", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
                                                                new Regex(@"[\s\x20-\x2f\x3a-\x40\x5b-\x60]+", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
                                                            };

    private const string ATTACH_PATTERN = @"\[(?:attach|attachimg)](.*?)\[\/(?:attach|attachimg)]";
    private static readonly Regex BbCodeAttachTagRegex = new(ATTACH_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);


    static RegexHelper()
    {
        var bbcodeDic = new Dictionary<string, Func<Match, int, Dictionary<int, string>, string>>();

        string GetBbcode(Match match, int tid, Dictionary<int, string> attachPathDic)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Value;
        }

        string GetAttachBbcode(Match match, int tid, Dictionary<int, string> attachPathDic)
        {
            var content = match.Groups["content"].Value;

            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var isInt = int.TryParse(content, out var aid);

            if (!isInt) return match.Value;

            var fullPath = attachPathDic.ContainsKey(aid) ? attachPathDic[aid] : string.Empty;

            return fullPath;
        }

        string GetUrlBbcode(Match match, int tid, Dictionary<int, string> attachPathDic)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Result("[url=${content}]${content}[/url]");
        }

        string GetNextMedia(Match match, int tid, Dictionary<int, string> attachPathDic)
        {
            var content = match.Groups["content"].Value;
            var attr = match.Groups["attr"].Value;

            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(attr))
                return match.Value;

            var attrs = attr.Split(',');

            if (attrs.Length < 2)
                return match.Value;

            return $"[{EMBED}]http://tw.nextmedia.com/playeriframe/articleplayer/IssueID/{attrs[0]}/Photo/{attrs[1]}.jpg/Video/{content}/Level/N/Artid//psecid/international/AdKey/realtimenews_international/Type/Realtimenews[/{EMBED}]";
        }

        bbcodeDic.Add("img", GetBbcode);
        bbcodeDic.Add("attach", GetAttachBbcode);
        bbcodeDic.Add("attachimg", GetAttachBbcode);
        bbcodeDic.Add("media", GetUrlBbcode);
        bbcodeDic.Add("youtube", (match, _, _) => match.Result($"[{EMBED}]https://www.youtube.com/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("facebook", (match, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("xhamster", (match, _, _) => match.Result($"[{EMBED}]https://xhamster.com/xembed.php?video=${{content}}[/{EMBED}]"));
        bbcodeDic.Add("youporn", (match, _, _) => match.Result($"[{EMBED}]http://www.youporn.com/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("twitter", (match, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("gfycat", (match, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("fbpost", (match, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("youjizz", (match, _, _) => match.Result($"[{EMBED}]https://www.youjizz.com/videos/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("ig", (match, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("avgle", (match, _, _) => match.Result($"[{EMBED}]https://avgle.com/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("av", (match, _, _) => match.Result($"[{EMBED}]https://av.jkforum.net/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("xvideos", (match, _, _) => match.Result($"[{EMBED}]https://flashservice.xvideos.com/embedframe/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("fc2", (match, _, _) => match.Result($"[{EMBED}]https://video.fc2.com/flv2.swf?i=${{content}}&d=1373&movie_stop=off&no_progressive=1&otag=1&sj=10&rel=1[/{EMBED}]"));
        bbcodeDic.Add("weibo", (match, _, _) => match.Result($"[{EMBED}]http://video.weibo.com/player/1034:${{content}}/v.swf[/{EMBED}]"));
        bbcodeDic.Add("youmaker", (match, _, _) => match.Result($"[{EMBED}]http://www.youmaker.com/video/v%3Fid%3D${{content}}%26nu%3Dnu&showdigits=true&overstretch=fit&autostart=false&rotatetime=15&linkfromdisplay=false&repeat=false&showfsbutton=false&fsreturnpage=&fullscreenpage=[/{EMBED}]"));
        bbcodeDic.Add("nextmedia", GetNextMedia);
        bbcodeDic.Add("wall", (match, _, _) => match.Result($"[{EMBED}]https://www.jkforum.net/home/ifr/${{content}}[/{EMBED}]"));

        BbcodeDic = bbcodeDic;

        var bbcodeKeys = string.Join("|", BbcodeDic.Keys);

        Pattern = $"\\[(?<tag>(?:attach)?{bbcodeKeys})=?(?<attr>[^\\]]*)\\](?<content>[^\\[]*)\\[\\/(?:(?:attach)?{bbcodeKeys})]" +
                  "|(?<emoji>{:([1-9]|10)_(199|[2-7][0-9]{2}|8[0-3][0-9]|84[0-6]):})";

        Regex = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    public static string GetNewMessage(string message, int tid, Dictionary<int, string> attachPathDic)
    {
        var newMessage = Regex.Replace(message, m =>
                                                {
                                                    if (!string.IsNullOrEmpty(m.Groups["emoji"].Value))
                                                        return string.Empty;

                                                    var tag = m.Groups["tag"].Value;

                                                    if (string.IsNullOrEmpty(tag))
                                                        return m.Value;

                                                    if (!BbcodeDic.ContainsKey(tag)) return m.Value;

                                                    var replacement = BbcodeDic[tag](m, tid, attachPathDic);

                                                    return replacement;
                                                });

        return newMessage;
    }

    public static string? CleanText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
                   ? null
                   : RegexTrims.Aggregate(text, (current, regex) => regex.Replace(current, " "));
    }


    public static Dictionary<int, string> GetAttachFileNameDic(IEnumerable<Post> posts)
    {
        var attachFileGroups = posts.GroupBy(x => x.Tid % 10,
                                             x => BbCodeAttachTagRegex.Matches(x.Message).Select(match =>
                                                                                                 {
                                                                                                     var content = match.Groups[1].Value;

                                                                                                     if (int.TryParse(content, out var attachmentId))
                                                                                                         return attachmentId;

                                                                                                     return -1;
                                                                                                 })).Where(x => x.Key != -1).ToArray();


        int[] GetValueByKey(int key) => attachFileGroups.FirstOrDefault(x => x.Key == key)?.SelectMany(x=>x).Distinct()?.ToArray() ?? new[] { -1 };
        
        using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        var attacheDic = cn.Query<(int, string, bool, bool)>($@"SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_1 WHERE aid IN @Groups1
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_2 WHERE aid IN @Groups2
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_3 WHERE aid IN @Groups3
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_4 WHERE aid IN @Groups4
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_5 WHERE aid IN @Groups5
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_6 WHERE aid IN @Groups6
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_7 WHERE aid IN @Groups7
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_8 WHERE aid IN @Groups8
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_9 WHERE aid IN @Groups9
                                                                                      UNION ALL
                                                                                      SELECT aid,attachment,remote,isimage 
                                                                                      FROM pre_forum_attachment_0 WHERE aid IN @Groups0"
                                                           , new
                                                             {
                                                                 Groups1 = GetValueByKey(1),
                                                                 Groups2 = GetValueByKey(2),
                                                                 Groups3 = GetValueByKey(3),
                                                                 Groups4 = GetValueByKey(4),
                                                                 Groups5 = GetValueByKey(5),
                                                                 Groups6 = GetValueByKey(6),
                                                                 Groups7 = GetValueByKey(7),
                                                                 Groups8 = GetValueByKey(8),
                                                                 Groups9 = GetValueByKey(9),
                                                                 Groups0 = GetValueByKey(0),
                                                             }).ToDictionary(x => x.Item1, x =>
                                                                                           {
                                                                                               var (_, path, isRemote, isImage) = x;

                                                                                               var fullPath = string.Concat(isRemote ? Setting.ATTACHMENT_URL : Setting.FORUM_URL, Setting.ATTACHMENT_PATH, path);

                                                                                               var tag = isImage ? "img" : "file";

                                                                                               return string.Concat("[", tag, "]", fullPath, "[/", tag, "]");
                                                                                           });

        return attacheDic;
    }
}