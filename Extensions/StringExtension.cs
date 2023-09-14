using System.Text;
using System.Text.RegularExpressions;

namespace ForumDataMigration.Extensions;

public static class StringExtension
{
    private static readonly Regex RegexImg = new("\\[(?<tag>(?:attach)?img)(?<attr>[^\\]]*)\\](?<content>[^\\[]+)\\[\\/(?:(?:attach)?img)]",
                                                 RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public static List<string> GetExternalImageUrls(this string content)
    {
        var imageUrls = new List<string>();
        var matches = RegexImg.Matches(content);

        foreach (Match match in matches)
        {
            var contentResult = match.Groups["content"].Value.TrimStart();

            if (contentResult.StartsWith("http") && !contentResult.Contains("www.mymypic.net"))
                imageUrls.Add(contentResult);
        }

        return imageUrls;
    }

    public static string ToCopyText(this string? str)
    {
        if (string.IsNullOrEmpty(str))
            return str.ToCopyValue();

        var sb = new StringBuilder(str);

        sb.Replace(Setting.D, "");
        sb.Replace("\\", @"\\");
        sb.Replace("\r", "\\r");
        sb.Replace("\n", "\\n");
        sb.Replace("\u0000", "");

        return sb.ToString();
    }

    public static string ToNewTags(this string tagStr)
    {
        if (string.IsNullOrWhiteSpace(tagStr) || tagStr == "0")
            return string.Empty;

        var newTagStr = "";
        var starPoint = 0;

        for (var i = 0; i < tagStr.Length; i++)
        {
            if (tagStr[i] == ',')
                starPoint = i + 1;

            if (tagStr[i] == '\t')
                newTagStr += string.Concat(tagStr.AsSpan(starPoint, i - starPoint), "\t");
        }

        newTagStr = newTagStr.TrimEnd('\t');

        return newTagStr;
    }
}