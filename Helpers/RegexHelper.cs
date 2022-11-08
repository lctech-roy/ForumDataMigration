using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;
using Netcorext.Algorithms;

namespace ForumDataMigration.Helpers;

public static class RegexHelper
{
    private const string EMBED = "embed";
    private static readonly Dictionary<string, Func<Match, int, long, long, Dictionary<(int, int), Attachment>, StringBuilder, StringBuilder, string>> BbcodeDic = new();
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
        string GetBbcode(Match match, int tid, long sourceId, long memberId, Dictionary<(int, int), Attachment> attachmentDic, StringBuilder attachmentSb, StringBuilder articleAttachmentSb)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Value;
        }

        string GetAttachBbcode(Match match, int tid, long sourceId, long memberId, Dictionary<(int, int), Attachment> attachmentDic, StringBuilder attachmentSb, StringBuilder articleAttachmentSb)
        {
            var content = match.Groups["content"].Value;

            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var isInt = int.TryParse(content, out var aid);

            if (!isInt) return match.Value;

            var attachment = attachmentDic.GetValueOrDefault((tid % 10, aid));

            if (attachment == null)
                return string.Empty;

            attachment.CreatorId = memberId;
            attachment.ModifierId = memberId;

            attachmentSb.AppendAttachmentValue(attachment);
            
            articleAttachmentSb.AppendValueLine(sourceId, attachment.Id,
                                                attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);

            return attachment.BbCode;
        }

        string GetUrlBbcode(Match match, int tid, long sourceId, long memberId, Dictionary<(int, int), Attachment> attachmentDic, StringBuilder attachmentSb, StringBuilder articleAttachmentSb)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Result("[url=${content}]${content}[/url]");
        }

        string GetNextMedia(Match match, int tid, long sourceId, long memberId, Dictionary<(int, int), Attachment> attachmentDic, StringBuilder attachmentSb, StringBuilder articleAttachmentSb)
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

        string RemoveUnUsedHideAttr(Match match, int tid, long sourceId, long memberId, Dictionary<(int, int), Attachment> attachmentDic, StringBuilder attachmentSb, StringBuilder articleAttachmentSb)
        {
            return string.IsNullOrWhiteSpace(match.Groups["attr"].Value) ? match.Value : match.Result("[hide]${content}[/hide]");
        }

        BbcodeDic.Add("img", GetBbcode);
        BbcodeDic.Add("attach", GetAttachBbcode);
        BbcodeDic.Add("attachimg", GetAttachBbcode);
        BbcodeDic.Add("media", GetUrlBbcode);
        BbcodeDic.Add("youtube", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://www.youtube.com/embed/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("facebook", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("xhamster", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://xhamster.com/xembed.php?video=${{content}}[/{EMBED}]"));
        BbcodeDic.Add("youporn", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]http://www.youporn.com/embed/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("twitter", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("gfycat", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("fbpost", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("youjizz", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://www.youjizz.com/videos/embed/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("ig", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("avgle", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://avgle.com/embed/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("av", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://av.jkforum.net/embed/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("xvideos", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://flashservice.xvideos.com/embedframe/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("fc2", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://video.fc2.com/flv2.swf?i=${{content}}&d=1373&movie_stop=off&no_progressive=1&otag=1&sj=10&rel=1[/{EMBED}]"));
        BbcodeDic.Add("weibo", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]http://video.weibo.com/player/1034:${{content}}/v.swf[/{EMBED}]"));
        BbcodeDic.Add("youmaker", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]http://www.youmaker.com/video/v%3Fid%3D${{content}}%26nu%3Dnu&showdigits=true&overstretch=fit&autostart=false&rotatetime=15&linkfromdisplay=false&repeat=false&showfsbutton=false&fsreturnpage=&fullscreenpage=[/{EMBED}]"));
        BbcodeDic.Add("nextmedia", GetNextMedia);
        BbcodeDic.Add("wall", (match, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://www.jkforum.net/home/ifr/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("hide", RemoveUnUsedHideAttr);

        var bbcodeKeys = string.Join("|", BbcodeDic.Keys);

        Pattern = $"\\[(?<tag>(?:attach)?{bbcodeKeys})=?(?<attr>[^\\]]*)\\](?<content>[^\\[]*)\\[\\/(?:(?:attach)?{bbcodeKeys})]" +
                  "|(?<emoji>{:([1-9]|10)_(199|[2-7][0-9]{2}|8[0-3][0-9]|84[0-6]):})";

        Regex = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    public static string GetNewMessage(string message, int tid, long sourceId, long memberId, Dictionary<(int, int), Attachment> attachmentDic, StringBuilder attachmentSb, StringBuilder articleAttachmentSb)
    {
        var newMessage = Regex.Replace(message, m =>
                                                {
                                                    if (!string.IsNullOrEmpty(m.Groups["emoji"].Value))
                                                        return string.Empty;

                                                    var tag = m.Groups["tag"].Value;

                                                    if (string.IsNullOrEmpty(tag))
                                                        return m.Value;

                                                    if (!BbcodeDic.ContainsKey(tag)) return m.Value;

                                                    var replacement = BbcodeDic[tag](m, tid, sourceId, memberId, attachmentDic, attachmentSb, articleAttachmentSb);

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

    public static IGrouping<int, IEnumerable<int>>[] GetAttachmentGroups(IEnumerable<ArticlePost> posts)
    {
        var attachFileGroups = posts.GroupBy(x => x.Tid % 10,
                                             x => BbCodeAttachTagRegex.Matches(x.Message).Select(match =>
                                                                                                 {
                                                                                                     var content = match.Groups[1].Value;

                                                                                                     if (int.TryParse(content, out var attachmentId))
                                                                                                         return attachmentId;

                                                                                                     return -1;
                                                                                                 })).Where(x => x.Key != -1).ToArray();

        return attachFileGroups;
    }

    public static IGrouping<int, IEnumerable<int>>[] GetAttachmentGroups(IEnumerable<CommentPost> posts)
    {
        var attachFileGroups = posts.GroupBy(x => x.Tid % 10,
                                             x => BbCodeAttachTagRegex.Matches(x.Content ?? "").Select(match =>
                                                                                                       {
                                                                                                           var content = match.Groups[1].Value;

                                                                                                           if (int.TryParse(content, out var attachmentId))
                                                                                                               return attachmentId;

                                                                                                           return -1;
                                                                                                       })).Where(x => x.Key != -1).ToArray();

        return attachFileGroups;
    }
}