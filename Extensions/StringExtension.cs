using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ForumDataMigration.Extensions;

public static class StringExtension
{
    private static readonly Regex RegexImg = new("\\[(?<tag>(?:attach)?img)(?<attr>[^\\]]*)\\](?<content>[^\\[]+)\\[\\/(?:(?:attach)?img)]", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    // private static readonly Regex RegexD = new($"\\{Setting.D}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // private static readonly Regex RegexU = new("\\u0000", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // private static readonly Regex RegexBackSlash = new("\\\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // private static readonly Regex RegexBackSlashR = new("\\r", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // private static readonly Regex RegexBackSlashN = new("\\n", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // public static string ToCopyText(this string str)
    // {
    //     return str.Length == 0 ? str : str.Replace(Setting.D,"")
    //                                       .Replace("\\","\\\\")
    //                                       .Replace("\r", "\\r")
    //                                       .Replace("\n", "\\n")
    //                                       .Replace("\u0000", "");
    // }

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
    
    public static string ToCopyText(this string str)
    {
        var sb = new StringBuilder(str);
    
        sb.Replace(Setting.D,"");
        sb.Replace("\\", "\\\\");
        sb.Replace("\r", "\\r");
        sb.Replace("\n", "\\n");
        sb.Replace("\u0000", "");
    
        return sb.ToString();
    }
    
    // public static string ToCopyText(this string str)
    // {
    //     RegexD.Replace(str,"");
    //     RegexU.Replace(str,"");
    //     RegexBackSlash.Replace(str,"\\\\");
    //     RegexBackSlashR.Replace(str,"\\r");
    //     RegexBackSlashN.Replace(str,"\\n");
    //
    //     return str;
    // }
    
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

        newTagStr = newTagStr[..^1];

        return newTagStr;
    }
}