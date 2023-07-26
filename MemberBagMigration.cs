using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Npgsql;

namespace ForumDataMigration;

public class MemberBagMigration
{
    private const string BAG_SQL = $"COPY \"{nameof(MemberBag)}\" " +
                                   $"(\"{nameof(MemberBag.Id)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string BAG_MEDAL_SQL = $"COPY \"{nameof(MemberBagItem)}\" " +
                                         $"(\"{nameof(MemberBagItem.Id)}\",\"{nameof(MemberBagItem.MedalId)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_BAG_MEDAL_ITEM_SQL = $@"SELECT inventory_id::INT AS Id, medal_id::INT AS MedalId, created_at AS CreationDate,created_at AS ModificationDate 
                                                      FROM medal_asset WHERE deleted_at IS NULL ORDER BY inventory_id, medal_id";

    public void Migration()
    {
        #region 轉檔前準備相關資料

        var medalIdHash = RelationHelper.GetMedalIdHash();

        #endregion

        MemberBagItem[] memberBagItems;

        using (var cn = new NpgsqlConnection(Setting.OLD_GAME_CENTER_CONNECTION))
        {
            memberBagItems = cn.Query<MemberBagItem>(QUERY_BAG_MEDAL_ITEM_SQL).ToArray();
        }

        var bagSb = new StringBuilder(BAG_SQL);
        var bagMedalItemSb = new StringBuilder(BAG_MEDAL_SQL);

        var previousBagItem = new MemberBagItem();

        foreach (var bagGameItem in memberBagItems)
        {
            if (bagGameItem.Id == 0) continue;

            if(!medalIdHash.Contains(bagGameItem.MedalId))
                continue;

            if (bagGameItem.Id != previousBagItem.Id)
                bagSb.AppendValueLine(bagGameItem.Id,
                                      bagGameItem.CreationDate, bagGameItem.CreatorId, bagGameItem.ModificationDate, bagGameItem.ModifierId, bagGameItem.Version);

            bagMedalItemSb.AppendValueLine(bagGameItem.Id, bagGameItem.MedalId,
                                           bagGameItem.CreationDate, bagGameItem.CreatorId, bagGameItem.ModificationDate, bagGameItem.ModifierId, bagGameItem.Version);

            previousBagItem = bagGameItem;
        }

        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(MemberBag)}.sql", bagSb.ToString());
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(MemberBagItem)}.sql", bagMedalItemSb.ToString());
    }
}