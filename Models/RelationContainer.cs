using ForumDataMigration.Helper;

namespace ForumDataMigration;

public static class RelationContainer
{
    public static Dictionary<int, long> ArticleIdDic = new();

    public static Dictionary<int, long> GetArticleIdDic()
    {
        return ArticleIdDic.Any() ? ArticleIdDic : RelationHelper.GetArticleDic();
    }
}