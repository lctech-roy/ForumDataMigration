using System.Collections.Concurrent;
using ForumDataMigration.Helper;
using ForumDataMigration.Models;
using Microsoft.VisualStudio.Threading;
using Npgsql;

namespace ForumDataMigration;

public static class RelationContainer
{
    public static Dictionary<int, long> ArticleIdDic = new();

    public static Dictionary<int, long> GetArticleIdDic()
    {
        return ArticleIdDic.Any() ? ArticleIdDic : RelationHelper.GetArticleDic();
    }
}