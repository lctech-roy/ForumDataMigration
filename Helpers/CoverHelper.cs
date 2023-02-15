using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ForumDataMigration.Helper;

public static class CoverHelper
{
    private const string FILE_EXTENSION = ".jpg";
    private const string LOCAL_COVER_URL = "https://www.jkforum.net/data/attachment/forum/threadcover/";
    private const string REMOTE_COVER_URL = "https://www.mymypic.net/data/attachment/forum/threadcover/";
    private const string LOCAL_THUMB_URL = "https://www.jkforum.net/data/attachment/threadicon/";
    private const string REMOTE_THUMB_URL = "https://www.mymypic.net/data/attachment/threadicon/";

    //1
    //0
    public static string GetCoverPath(int tid, string cover)
    {
        if (cover is "" or "0")
            return string.Empty;

        var coverElements = cover.Split('_');

        var coverPath = int.Parse(coverElements[0]) < 0 ? REMOTE_COVER_URL : LOCAL_COVER_URL;

        string hash;

        using (var md5 = MD5.Create())
        {
            var result = md5.ComputeHash(Encoding.UTF8.GetBytes(tid.ToString()));
            var strResult = BitConverter.ToString(result);
            hash = strResult.Replace("-", "").ToLower();
        }

        coverPath = string.Concat(coverPath, $"{hash[..2]}/{hash.Substring(2, 2)}/");

        var fileName = coverElements.Length == 1 ? $"{tid}{FILE_EXTENSION}" : $"{coverElements[1]}{FILE_EXTENSION}";

        coverPath = string.Concat(coverPath, fileName);

        return coverPath;
    }

    public static string GetThumbPath(int tid, string thumb)
    {
        if (thumb is "" or "0")
            return string.Empty;

        var thumbElements = thumb.Split('_');

        var thumbType = thumbElements[0];

        var thumbPath = thumbType switch
                        {
                            "1" => "." + LOCAL_THUMB_URL,
                            "4" => REMOTE_THUMB_URL,
                            _ => string.Empty
                        };

        if (thumbPath == string.Empty)
            return string.Empty;

        var subPath = Math.Ceiling(tid / 3000D).ToString(CultureInfo.InvariantCulture).PadLeft(4, '0') + "/";

        thumbPath = string.Concat(thumbPath, subPath);

        var fileName = thumbElements.Length == 1 ? $"{tid}{FILE_EXTENSION}" : $"{thumbElements[1]}{FILE_EXTENSION}";

        thumbPath = string.Concat(thumbPath, fileName);

        return thumbPath;
    }
}