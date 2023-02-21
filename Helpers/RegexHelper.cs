using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;

namespace ForumDataMigration.Helpers;

public static class RegexHelper
{
    private const string EMBED = "embed";
    private static readonly Dictionary<string, Func<Match, int, int, long?, long, DateTimeOffset, StringBuilder, StringBuilder, bool, string>> BbcodeDic = new();
    private static string Pattern { get; }
    private static Regex MessageRegex { get; }

    // for es xml
    // private static readonly IEnumerable<Regex> RegexTrims = new[]
    //                                                         {
    //                                                             new Regex(@"<[^>]+>|\[[^\]]+\]", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
    //                                                             new Regex(@"[（）【】「」『』《》：；！？，。｜、～·﹍——]+", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
    //                                                             new Regex(@"[\s\x20-\x2f\x3a-\x40\x5b-\x60]+", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase),
    //                                                         };

    private const string ID = "Id";
    private const string ID_PATTERN = $@"^(?<{ID}>[\w]*).*";

    private const string SUBJECT_PATTERN = @"\s";

    private static readonly Regex IdRegex = new(ID_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SubjectRegex = new(SUBJECT_PATTERN, RegexOptions.Compiled);
    private static readonly (Dictionary<string, long> pathIdDic, Dictionary<long, List<Attachment>> attachmentDic) ArtifactAttachmentTuple;
    private static readonly Dictionary<int, Dictionary<int, Dictionary<int, bool>>> AttachmentTableDic;

    static RegexHelper()
    {
        AttachmentTableDic = AttachmentHelper.GetAttachmentTableDic();
        ArtifactAttachmentTuple = AttachmentHelper.GetArtifactAttachmentDic();

        string GetBbcode(Match match, int tableNumber, int pid, long? sourceId, long memberId, DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb, bool isComment)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Value;
        }

        string GetAttachBbcode(Match match, int tableNumber, int pid, long? sourceId, long memberId, DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb, bool isComment)
        {
            var content = match.Groups["content"].Value;

            //第二層回覆沒有附件
            if (!sourceId.HasValue)
                return match.Value;

            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var isInt = int.TryParse(content, out var oldAid);

            if (!isInt) return match.Value;

            var attachmentDic = AttachmentTableDic[tableNumber].GetValueOrDefault(pid);

            var newAid = oldAid * 10 + tableNumber;

            if (attachmentDic == null || !attachmentDic.ContainsKey(newAid))
                return string.Empty;
            
            sourceAttachmentSb.AppendValueLine(isComment ? sourceId * 10 : sourceId, newAid, creationDate, memberId, creationDate, memberId, 0);

            var isImage = attachmentDic[newAid];

            //避免產生重複的attachmentId
            attachmentDic.Remove(newAid);

            return AttachmentHelper.GetBbcode(newAid, isImage);
        }

        string GetUrlBbcode(Match match, int tableNumber, int pid, long? sourceId, long memberId, DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb, bool isComment)
        {
            return string.IsNullOrWhiteSpace(match.Groups["content"].Value) ? string.Empty : match.Result("[url=${content}]${content}[/url]");
        }

        string RemoveUnUsedHideAttr(Match match, int tableNumber, int pid, long? sourceId, long memberId, DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb, bool isComment)
        {
            return string.IsNullOrWhiteSpace(match.Groups["attr"].Value) ? match.Value : match.Result("[hide]${content}[/hide]");
        }

        string GetYoutube(Match match, int tableNumber, int pid, long? sourceId, long memberId, DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb, bool isComment)
        {
            var content = match.Groups["content"].Value;

            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var replacement = IdRegex.Replace(content, innerMatch =>
                                                       {
                                                           var value = innerMatch.Groups[ID].Value;

                                                           return string.IsNullOrEmpty(value) ? string.Empty : $"[{EMBED}]https://youtu.be/{value}[/{EMBED}]";
                                                       });

            return replacement;
        }

        string GetVideo(Match match, int tableNumber, int pid, long? sourceId, long memberId, DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb, bool isComment)
        {
            var content = match.Groups["content"].Value;

            //第二層回覆沒有附件
            if (!sourceId.HasValue)
                return content;

            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var objectName = content.Replace(Setting.VIDEO_CDN, "");

            var parentId = ArtifactAttachmentTuple.pathIdDic.GetValueOrDefault(objectName);

            if (parentId == default)
                return match.Value;

            var attachments = ArtifactAttachmentTuple.attachmentDic[parentId];

            foreach (var attachment in attachments)
            {
                attachment.StoragePath = Path.GetDirectoryName(attachment.ObjectName);
                attachment.CreatorId = memberId;
                attachment.ModifierId = memberId;

                attachmentSb.AppendAttachmentValue(attachment);

                sourceAttachmentSb.AppendValueLine( isComment ? sourceId * 10 : sourceId, attachment.Id,
                                                   attachment.CreationDate, attachment.CreatorId, attachment.ModificationDate, attachment.ModifierId, attachment.Version);
            }

            //避免產生重複的attachmentId
            ArtifactAttachmentTuple.pathIdDic.Remove(objectName);

            var attr = match.Groups["attr"].Value;

            var replacement = $"[video={attr}]{parentId}[/video]";

            return replacement;
        }

        BbcodeDic.Add("img", GetBbcode);
        BbcodeDic.Add("attach", GetAttachBbcode);
        BbcodeDic.Add("attachimg", GetAttachBbcode);
        BbcodeDic.Add("media", GetUrlBbcode);
        BbcodeDic.Add("video", GetVideo);

        #region embed part

        BbcodeDic.Add("youtube", GetYoutube);
        BbcodeDic.Add("facebook", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("fbpost", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("twitter", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));
        BbcodeDic.Add("av", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://av.jkforum.net/watch/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("avgle", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://avgle.com/video/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("xvideos", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://www.xvideos.com/video${{content}}[/{EMBED}]"));
        BbcodeDic.Add("youjizz", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://www.youjizz.com/videos/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("xhamster", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://zh.xhamster.com/videos/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("pornhub", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://pornhub.com/view_video.php?viewkey=${{content}}[/{EMBED}]"));
        BbcodeDic.Add("tiktok", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]https://www.tiktok.com/${{attr}}/video/${{content}}[/{EMBED}]"));
        BbcodeDic.Add("ig", (match, _, _, _, _, _, _, _, _) => match.Result($"[{EMBED}]${{content}}[/{EMBED}]"));

        #endregion

        BbcodeDic.Add("hide", RemoveUnUsedHideAttr);
        BbcodeDic.Add("i", (_, _, _, _, _, _, _, _, _) => string.Empty); //[i=s] 本篇最後由 why5684784why 於 2017-5-16 02:41 編輯 [/i] => 整段拿掉
        BbcodeDic.Add("tr", (match, _, _, _, _, _, _, _, _) => match.Result($"[tr]${{content}}[/tr]"));
        BbcodeDic.Add("td", (match, _, _, _, _, _, _, _, _) => match.Result($"[td]${{content}}[/td]"));

        var bbcodeKeys = string.Join("|", BbcodeDic.Keys);

        Pattern = $"\\[(?<tag>(?:attach)?{bbcodeKeys})=?(?<attr>[^\\]]*)\\](?<content>[^\\[]*)\\[\\/(?:(?:attach)?{bbcodeKeys})]" +
                  "|(?<emoji>{:([1-9]|10)_(199|[2-7][0-9]{2}|8[0-3][0-9]|84[0-6]):})";

        MessageRegex = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    public static string GetNewMessage(string message, int tableNumber, int pid, long? sourceId, long memberId,
                                       DateTimeOffset creationDate, StringBuilder attachmentSb, StringBuilder sourceAttachmentSb,
                                       bool isComment = false)
    {
        var newMessage = MessageRegex.Replace(message, m =>
                                                       {
                                                           if (!string.IsNullOrEmpty(m.Groups["emoji"].Value))
                                                               return string.Empty;

                                                           var tag = m.Groups["tag"].Value;

                                                           if (string.IsNullOrEmpty(tag))
                                                               return m.Value;


                                                           if (!BbcodeDic.ContainsKey(tag)) return m.Value;

                                                           var replacement = BbcodeDic[tag](m, tableNumber, pid, sourceId, memberId, creationDate, attachmentSb, sourceAttachmentSb, isComment);

                                                           return replacement;
                                                       });

        //第二層回覆沒有附件
        if (!sourceId.HasValue)
            return newMessage;

        var attachments = AttachmentTableDic[tableNumber].GetValueOrDefault(pid);

        if (!attachments?.Any() ?? true)
            return newMessage;


        var newMessageSb = new StringBuilder(newMessage);

        foreach (var (aid, isImage) in attachments)
        {
            newMessageSb.Append(Environment.NewLine);
            newMessageSb.Append(AttachmentHelper.GetBbcode(aid, isImage));

            sourceAttachmentSb.AppendValueLine(isComment ? sourceId * 10 : sourceId, aid, creationDate, memberId, creationDate, memberId, 0);
        }

        return newMessageSb.ToString();
    }

    //xml用
    // public static string? CleanText(string? text)
    // {
    //     return string.IsNullOrWhiteSpace(text)
    //                ? null
    //                : RegexTrims.Aggregate(text, (current, regex) => regex.Replace(current, " "));
    // }

    public static string GetNewSubject(string subject)
    {
        subject = WebUtility.HtmlDecode(subject);
        subject = WebUtility.HtmlEncode(subject);

        return SubjectRegex.Replace(subject, " ");
    }
}