using System.Text.RegularExpressions;

namespace ForumDataMigration.Helpers;

public static class SerializeHelper
{
    private static readonly Regex ContentRegex = new(@"^(?!\s*$).+\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    public static string GetTitle(string content)
    {
        var breakLine = Environment.NewLine.ToCharArray();

        content = content.Trim().TrimStart(breakLine);

        var regexResult = ContentRegex.Match(content).ToString().TrimEnd(breakLine);

        return regexResult.Length > 40 ? regexResult[..40] : regexResult;
    }
}