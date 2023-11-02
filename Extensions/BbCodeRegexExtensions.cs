using System.Text.RegularExpressions;

namespace ForumDataMigration.Extensions;

public static class BbCodeRegexExtensions
{
    private const string HIDE_PATTERN = @"\[(hide)[^\]]*]([\s\S]*)\[\/\1]";
    private const string FREE_CONTENT_PATTERN = @"\[(free)[^\]]*]([\s\S]*)\[\/\1]";
    private const string URL_PATTERN = @"\[(img|attachimg|attach|media|video|url|embed|code|file|fbpost)[^\]]*](.*?)\[\/\1]";
    private const string FONT_PATTERN = @"\[\/?(?:font|size|hr|p|table|tr|td|b|u|i|backcolor|color|align|list|quote|import)[^\]]*\]|{[^}]*}|[\f\r\n\v\t]";

    private static readonly Regex HideRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FreeContentRegex = new(FREE_CONTENT_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UrlRegex = new(URL_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FontRegex = new(FONT_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ContentRegex = new(@"^(?!\s*$).+\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static string RemoveHideContent(this string content)
    {
        return HideRegex.Replace(content, "");
    }

    public static string GetFreeContent(this string content)
    {
        return FreeContentRegex.Matches(content).FirstOrDefault()?.Groups[2].Value ?? string.Empty;
    }

    public static string GetContentSummary(this string content)
    {
        var result = UrlRegex.Replace(content, "");
        result = FontRegex.Replace(result, "");
        result = result[..(result.Length >= 200 ? 200 : result.Length)];

        return result;
    }
    
    public static string GetTitle(this string content)
    {
        var breakLine = Environment.NewLine.ToCharArray();

        content = content.Trim().TrimStart(breakLine);

        var result = ContentRegex.Match(content).ToString().TrimEnd(breakLine);
        
        result = UrlRegex.Replace(result, "");
        result = FontRegex.Replace(result, "");

        return result.Length > 40 ? result[..40] : result;
    }
}