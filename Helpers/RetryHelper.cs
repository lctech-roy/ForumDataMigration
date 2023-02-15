using System.Globalization;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Lctech.Comment.Domain.Entities;
using Lctech.Jkf.Forum.Domain.Entities;
using MySqlConnector;
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

    public static void SetArticleRetry(string folderName, string? fileName, string exception)
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        cn.Execute(@"UPDATE ""ArticleRetry"" SET ""FolderName"" = @folderName, ""FileName"" = @fileName, ""Exception"" = @exception", new { folderName, fileName, exception });
    }

    public static string? GetArticleRetryDateStr()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_FORUM_CONNECTION);

        return cn.QueryFirst<string?>(@"SELECT ""FolderName"" FROM ""ArticleRetry""");
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

    public static void SetCommentRetry(string folderName, string? fileName, string exception)
    {
        using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        cn.Execute(@"UPDATE ""CommentRetry"" SET ""FolderName"" = @folderName, ""FileName"" = @fileName, ""Exception"" = @exception", new { folderName, fileName, exception });
    }

    public static string? GetCommentRetryDateStr()
    {
        using var cn = new NpgsqlConnection(Setting.NEW_COMMENT_CONNECTION);

        return cn.QueryFirst<string?>(@"SELECT ""FolderName"" FROM ""CommentRetry""");
    }

    public static void RemoveFilesByDate(IEnumerable<string> rootPaths, string dateFolderName)
    {
        var periods = PeriodHelper.GetPeriods(dateFolderName);

        foreach (var rootPath in rootPaths)
        {
            foreach (var period in periods)
            {
                var path = $"{rootPath}/{period.FolderName}";

                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
        }
    }

    public static void RemoveFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public static string GetEarliestCreateDateStr()
    {
        using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        var createSeconds = cn.QueryFirst<long>(@"SELECT dateline FROM pre_forum_thread WHERE tid = (SELECT MIN(tid) FROM thread_last_updated)");

        var dateTime = DateTimeOffset.FromUnixTimeSeconds(createSeconds);

        var dateStr = PeriodHelper.ConvertToDateStr(dateTime);

        return dateStr;
    }

    public static void RemoveDataByDateStr(string connectionStr, string tableName, string dateStr)
    {
        CultureInfo provider = CultureInfo.InvariantCulture;
        const string format = "yyyyMM";

        var creationDate = DateTime.ParseExact(dateStr, format, provider);
        var creationDateOffset = new DateTimeOffset(creationDate, TimeSpan.Zero);
        
        using var cn = new NpgsqlConnection(connectionStr);

        var affectedRowCount = cn.Execute($@"DELETE FROM ""{tableName}"" WHERE ""CreationDate"" >= @creationDateOffset", new { creationDateOffset });

        Console.WriteLine($"Remove {tableName} Count:{affectedRowCount}");
    }
}