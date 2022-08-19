using System.Text.RegularExpressions;
using Dapper;

namespace ForumDataMigration.Helper;

public static class RegexHelper
{
    private const string EMBED = "embed";
    public static Dictionary<string, Func<Match, int, string>> BbcodeDic { get; set; }
    public static string Pattern { get; }
    public static Regex Regex { get; }

    static RegexHelper()
    {
        var bbcodeDic = new Dictionary<string, Func<Match, int, string>>();

        string GetBbcode(Match match, int tid)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Value;
        }

        string GetAttachBbcode(Match match, int tid)
        {
            var content = match.Groups["content"].Value;

            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var isInt = int.TryParse(content, out var aid);

            if (!isInt) return match.Value;

            var attachmentTableId = tid % 10;

            using var cn = new MySqlConnector.MySqlConnection(Setting.OLD_FORUM_CONNECTION);

            var (path, isRemote, isImage) = cn.QueryFirstOrDefault<(string, bool, bool)>($"SELECT attachment,remote,isimage FROM pre_forum_attachment_{attachmentTableId} WHERE aid = @aid", new { aid });

            path = string.Concat(isRemote ? Setting.ATTACHMENT_URL : Setting.FORUM_URL, Setting.ATTACHMENT_PATH, path);

            var tag = isImage ? "img" : "file";

            return $"[{tag}]{path}[/{tag}]";
        }

        string GetUrlBbcode(Match match, int tid)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Result("[url=${content}]${content}[/url]");
        }

        string GetNextMedia(Match match, int tid)
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
        bbcodeDic.Add("youtube", (match, _) => match.Result($"[{EMBED}]https://www.youtube.com/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("facebook", (match, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("xhamster", (match, _) => match.Result($"[{EMBED}]https://xhamster.com/xembed.php?video=${{content}}[/{EMBED}]"));
        bbcodeDic.Add("youporn", (match, _) => match.Result($"[{EMBED}]http://www.youporn.com/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("twitter", (match, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("gfycat", (match, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("fbpost", (match, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("youjizz", (match, _) => match.Result($"[{EMBED}]https://www.youjizz.com/videos/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("ig", (match, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        bbcodeDic.Add("avgle", (match, _) => match.Result($"[{EMBED}]https://avgle.com/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("av", (match, _) => match.Result($"[{EMBED}]https://av.jkforum.net/embed/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("xvideos", (match, _) => match.Result($"[{EMBED}]https://flashservice.xvideos.com/embedframe/${{content}}[/{EMBED}]"));
        bbcodeDic.Add("fc2", (match, _) => match.Result($"[{EMBED}]https://video.fc2.com/flv2.swf?i=${{content}}&d=1373&movie_stop=off&no_progressive=1&otag=1&sj=10&rel=1[/{EMBED}]"));
        bbcodeDic.Add("weibo", (match, _) => match.Result($"[{EMBED}]http://video.weibo.com/player/1034:${{content}}/v.swf[/{EMBED}]"));
        bbcodeDic.Add("youmaker", (match, _) => match.Result($"[{EMBED}]http://www.youmaker.com/video/v%3Fid%3D${{content}}%26nu%3Dnu&showdigits=true&overstretch=fit&autostart=false&rotatetime=15&linkfromdisplay=false&repeat=false&showfsbutton=false&fsreturnpage=&fullscreenpage=[/{EMBED}]"));
        bbcodeDic.Add("nextmedia", GetNextMedia);
        bbcodeDic.Add("wall", (match, _) => match.Result($"[{EMBED}]https://www.jkforum.net/home/ifr/${{content}}[/{EMBED}]"));
        
        BbcodeDic = bbcodeDic;

        var bbcodeKeys = string.Join("|", BbcodeDic.Keys);
        Pattern = $"\\[(?<tag>(?:attach)?{bbcodeKeys})=?(?<attr>[^\\]]*)\\](?<content>[^\\[]*)\\[\\/(?:(?:attach)?{bbcodeKeys})]" +
                  "|(?<emoji>{:([1-9]|10)_(199|[2-7][0-9]{2}|8[0-3][0-9]|84[0-6]):})";
        Regex = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }


    public static string GetNewMessage(string message, int tid)
    {
        var newMessage = Regex.Replace(message, m =>
                                                {
                                                    if (!string.IsNullOrEmpty(m.Groups["emoji"].Value))
                                                        return string.Empty;
                                                    
                                                    var tag = m.Groups["tag"].Value;

                                                    if (string.IsNullOrEmpty(tag))
                                                        return m.Value;

                                                    if (!BbcodeDic.ContainsKey(tag)) return m.Value;

                                                    var replacement = BbcodeDic[tag](m, tid);

                                                    return replacement;
                                                });

        return newMessage;
    }
}