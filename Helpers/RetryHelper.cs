using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Npgsql;

namespace ForumDataMigration.Helpers;

public static class RetryHelper
{
    private const string CREATE_RETRY_FILE_NAME = "CreateRetryTable.sql";
    private const string DROP_RETRY_FILE_NAME = "DropRetryTable.sql";
    private const string SCHEMA_PATH = "../../../ScriptSchema";

    public static void CreateArticleRetryTable()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(Article)}/{CREATE_RETRY_FILE_NAME}");
    }

    public static void DropArticleRetryTable()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(Article)}/{DROP_RETRY_FILE_NAME}");
    }

    public static void SetArticleRetry(string folderName,string? fileName, string exception)
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        cn.Execute(@"UPDATE ""ArticleRetry"" SET ""FolderName"" = @folderName, ""FileName"" = @fileName, ""Exception"" = @exception", new { folderName, fileName, exception });
    }

    public static (string? folderName, string? fileName) GetArticleRetry()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        return cn.QueryFirst<(string?, string?)>(@"SELECT ""FolderName"",""FileName"" FROM ""ArticleRetry""");
    }


    public static void CreateCommentRetryTable()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(Comment)}/{CREATE_RETRY_FILE_NAME}");
    }

    public static void DropCommentRetryTable()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        cn.ExecuteCommandByPath($"{SCHEMA_PATH}/{nameof(Comment)}/{DROP_RETRY_FILE_NAME}");
    }

    public static void SetCommentRetry(string folderName,string? fileName, string exception)
    {
        using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        cn.Execute(@"UPDATE ""CommentRetry"" SET ""FolderName"" = @folderName, ""FileName"" = @fileName, ""Exception"" = @exception", new { folderName, fileName, exception });
    }

    public static (string? folderName, string? fileName) GetCommentRetry()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        return cn.QueryFirst<(string?, string?)>(@"SELECT ""FolderName"",""FileName"" FROM ""CommentRetry""");
    }

    public static void RemoveFilesByDate(IEnumerable<string> rootPaths, string dateFolderName)
    {
        foreach (var rootPath in rootPaths)
        {
            var path = $"{rootPath}/{dateFolderName}"; 
            var directoryInfo = new DirectoryInfo(path);

            foreach (var file in directoryInfo.GetFiles())
                file.Delete();
        }
    }
}