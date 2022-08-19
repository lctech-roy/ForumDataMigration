using System.Text.RegularExpressions;
using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration;

public static class Setting
{
    public const string INSERT_DATA_PATH = "../../../ScriptInsert";

    public const string OLD_FORUM_CONNECTION = "Host=35.194.153.253;Port=3306;Username=testuser;Password=b5GbvjRKXhrXcuW;Database=newjk;Pooling=True;maximumpoolsize=80;default command timeout=300;TreatTinyAsBoolean=false;";
    // public const string NEW_FORUM_CONNECTION_LOCAL = "Host=127.0.0.1;Port=5432;Username=postgres;Password=P@ssw0rd;Database=lctech_jkf_forum_tttt;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;";
    public const string NEW_FORUM_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_jkf_forum;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_MEMBER_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_jkf_member;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_COMMENT_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_comment;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";


    //;Write Buffer Size=40000;Read Buffer Size=40000
    //;Write Buffer Size=40000;Read Buffer Size=40000
    public const string D = "";
    public const string COPY_NULL = "\\N";
    public const string FORUM_URL = "https://www.jkforum.net/";
    public const string ATTACHMENT_URL = "https://www.mymypic.net/";
    public const string ATTACHMENT_PATH = "data/attachment/forum/";
    
    private const string IMAGE_PATTERN = @"\[(?:img|attachimg)](.*?)\[\/(?:img|attachimg)]";
    private const string VIDEO_PATTERN = @"\[(media[^\]]*|video)](.*?)\[\/(media|video)]";
    private const string HIDE_PATTERN = @"(\[\/?hide[^\]]*\]|{[^}]*})";

    public static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static readonly Regex BbCodeHideTagRegex = new(HIDE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    
    public static readonly Dictionary<int, string?> ColorDic = new()
                                                               {
                                                                   { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                                                               };

    public const string COPY_ENTITY_SUFFIX = $",\"{nameof(Entity.CreationDate)}\",\"{nameof(Entity.CreatorId)}\",\"{nameof(Entity.ModificationDate)}\",\"{nameof(Entity.ModifierId)}\",\"{nameof(Entity.Version)}\") " +
                                      $"FROM STDIN (DELIMITER '{D}')\n";
    
    public const string COPY_SUFFIX = $") FROM STDIN (DELIMITER '{D}')\n";
    
    public const string EXTEND_DATA_RECOMMEND_COMMENT = "RecommendComment";
    public const string EXTEND_DATA_BOARD_ID = "BoardId";
    public const string COMMENT_EXTEND_DATA = "CommentExtendData";
}