using ForumDataMigration.Models;
using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration;

public static class Setting
{
    public const string OLD_FORUM_CONNECTION = "Host=35.194.153.253;Port=3306;Username=testuser;Password=b5GbvjRKXhrXcuW;Database=newjk;Pooling=True;maximumpoolsize=80;default command timeout=300;TreatTinyAsBoolean=false;sslmode=none;";

    //34.80.83.8
    // public const string NEW_FORUM_CONNECTION_LOCAL = "Host=127.0.0.1;Port=5432;Username=postgres;Password=P@ssw0rd;Database=lctech_jkf_forum_tttt;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;";
    public const string OLD_GAME_CENTER_CONNECTION = "Host=104.199.218.6;Port=5432;Username=postgres;Password=6qfh.d[(^%Dj2S7K;Database=jkfapi;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";

    private const string HOST = "104.199.140.32";
    private const string PORT = "5432";
    private const string PASSWORD = "fybfe9-xaTdon-dozziw";
    public const string NEW_FORUM_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_jkf_forum;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_COMMENT_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_comment;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_GAME_CENTER_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_jkf_gamecenter;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_GAME_CENTER_MEDAL_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_jkf_gamecenter_medal;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_ATTACHMENT_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_attachment;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_ARTIFACT_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_artifact_test;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_PARTICIPLE_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_participle;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_MEMBER_CONNECTION = $"Host={HOST};Port={PORT};Username=postgres;Password={PASSWORD};Database=lctech_jkf_member;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";


    public const string D = "";
    public const string FORUM_URL = "https://www.jkforum.net/";
    public const string ATTACHMENT_URL = "https://www.mymypic.net/";
    public const string VIDEO_CDN = "https://cdn.mymyatt.net/";
    public const string FORUM_ATTACHMENT_PATH = "data/attachment/forum/";
    public const string INSERT_DATA_PATH = "../../../ScriptInsert";
    public const string ATTACHMENT_PATH = $"{INSERT_DATA_PATH}/{nameof(Attachment)}";

    public const string COPY_ENTITY_SUFFIX = $",\"{nameof(Entity.CreationDate)}\",\"{nameof(Entity.CreatorId)}\",\"{nameof(Entity.ModificationDate)}\",\"{nameof(Entity.ModifierId)}\",\"{nameof(Entity.Version)}\") " +
                                             $"FROM STDIN (DELIMITER '{D}')\n";

    public const string COPY_SUFFIX = $") FROM STDIN (DELIMITER '{D}')\n";

    public const string SCHEMA_PATH = "../../../ScriptSchema";
    public const string BEFORE_FILE_NAME = "BeforeCopy.sql";
    public const string AFTER_FILE_NAME = "AfterCopy.sql";

    public const bool USE_UPDATED_DATE = false;

    public const string ATTACHMENT_START_DATE = "2007-12-01";

    public static long? TestTid = null;
}