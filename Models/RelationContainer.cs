using ForumDataMigration.Helpers;

namespace ForumDataMigration.Models;

public static class RelationContainer
{
    public static readonly HashSet<long> ArticleIdDic = new();

    public static HashSet<long> GetArticleIdHash()
    {
        return ArticleIdDic.Any() ? ArticleIdDic : RelationHelper.GetArticleIdHash();
    }
}