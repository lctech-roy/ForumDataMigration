using ForumDataMigration.Helper;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Netcorext.Extensions.Commons;

namespace ForumDataMigration;

public class MigrationCheck
{
    private static readonly HashSet<long> BoardIdHash = RelationHelper.GetBoardIdHash();
    private static readonly HashSet<long> CategoryIdHash = RelationHelper.GetCategoryIdHash();
    private static readonly HashSet<long> ProhibitMemberIdHash = MemberHelper.GetProhibitMemberIdHash();
    private static readonly Dictionary<(int, string), int?> ModDic = ArticleHelper.GetModDic();
    private static readonly Dictionary<long, Read> ReadDic = ArticleHelper.GetReadDic();
    private static readonly Dictionary<long, ArticleDeletion> ArticleDeletionDic = ArticleHelper.GetArticleDeletionDic();
    private static readonly Dictionary<string, long> MemberNameDic = MemberHelper.GetMemberNameDic();

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        if(BoardIdHash.IsEmpty())
            Console.WriteLine(nameof(BoardIdHash) + " Not found");
        if(CategoryIdHash.IsEmpty())
            Console.WriteLine(nameof(CategoryIdHash) + " Not found");
        if(ProhibitMemberIdHash.IsEmpty())
            Console.WriteLine(nameof(ProhibitMemberIdHash) + " Not found");
        if(ModDic.IsEmpty())
            Console.WriteLine(nameof(ModDic) + " Not found");
        if(ReadDic.IsEmpty())
            Console.WriteLine(nameof(ReadDic) + " Not found");
        if(ArticleDeletionDic.IsEmpty())
            Console.WriteLine(nameof(ArticleDeletionDic) + " Not found");
        if(MemberNameDic.IsEmpty())
            Console.WriteLine(nameof(MemberNameDic) + " Not found");
    }
}