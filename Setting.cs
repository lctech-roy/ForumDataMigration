using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration;

public static class Setting
{
    public const string OLD_FORUM_CONNECTION = "Host=35.194.153.253;Port=3306;Username=testuser;Password=b5GbvjRKXhrXcuW;Database=newjk;Pooling=True;maximumpoolsize=80;default command timeout=300;TreatTinyAsBoolean=false;";
    // public const string NEW_FORUM_CONNECTION_LOCAL = "Host=127.0.0.1;Port=5432;Username=postgres;Password=P@ssw0rd;Database=lctech_jkf_forum_tttt;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;";
    public const string OLD_GAME_CENTER_CONNECTION = "Host=104.199.218.6;Port=5432;Username=postgres;Password=6qfh.d[(^%Dj2S7K;Database=jkfapi;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";

    
    public const string NEW_FORUM_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_jkf_forum;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_MEMBER_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_jkf_member;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_COMMENT_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_comment;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    public const string NEW_GAME_CENTER_CONNECTION = "Host=35.234.11.73;Port=5432;Username=postgres;Password=fybfe9-xaTdon-dozziw;Database=lctech_jkf_gamecenter;Timeout=1024;CommandTimeout=1800;Maximum Pool Size=80;SSL Mode=Require;Trust Server Certificate=true;Include Error Detail=true";
    
    public const string D = "";
    public const string FORUM_URL = "https://www.jkforum.net/";
    public const string ATTACHMENT_URL = "https://www.mymypic.net/";
    public const string ATTACHMENT_PATH = "data/attachment/forum/";
    public const string INSERT_DATA_PATH = "../../../ScriptInsert";
    
    public const string COPY_ENTITY_SUFFIX = $",\"{nameof(Entity.CreationDate)}\",\"{nameof(Entity.CreatorId)}\",\"{nameof(Entity.ModificationDate)}\",\"{nameof(Entity.ModifierId)}\",\"{nameof(Entity.Version)}\") " +
                                             $"FROM STDIN (DELIMITER '{D}')\n";
    
    public const string COPY_SUFFIX = $") FROM STDIN (DELIMITER '{D}')\n";
}