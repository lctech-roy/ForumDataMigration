using System.Text;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helpers;
using ForumDataMigration.Models;
using Netcorext.Algorithms;
using Npgsql;

namespace ForumDataMigration;

public class GameItemMigration
{
    private const string BAG_SQL = $"COPY \"{nameof(Bag)}\" " +
                                   $"(\"{nameof(Bag.Id)}\",\"{nameof(Bag.Limit)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string BAG_GAME_ITEM_SQL = $"COPY \"{nameof(BagGameItem)}\" " +
                                             $"(\"{nameof(BagGameItem.Id)}\",\"{nameof(BagGameItem.GameItemId)}\",\"{nameof(BagGameItem.Quantity)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string QUERY_BAG_GAME_ITEM_SQL = $@"SELECT inventory_id::INT AS Id, material_id::INT AS GameItemId, stock AS Quantity, created_at AS CreationDate, updated_at AS ModificationDate 
                                                      FROM material_asset ORDER BY inventory_id, material_id";

    private readonly ISnowflake _snowflake;

    public GameItemMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        #region 轉檔前準備相關資料

        var gameItemRelationDic = RelationHelper.GetGameItemRelationDic();

        #endregion

        BagGameItem[] bagItems;

        using (var cn = new NpgsqlConnection(Setting.OLD_GAME_CENTER_CONNECTION))
        {
            bagItems = cn.Query<BagGameItem>(QUERY_BAG_GAME_ITEM_SQL).ToArray();
        }

        var bagSb = new StringBuilder(BAG_SQL);
        var bagItemSb = new StringBuilder(BAG_GAME_ITEM_SQL);

        var previousBagItem = new BagGameItem();

        foreach (var bagGameItem in bagItems)
        {
            if (bagGameItem.Id == 0) continue;

            bagGameItem.GameItemId = gameItemRelationDic.GetValueOrDefault(bagGameItem.GameItemId);

            if (bagGameItem.GameItemId == 0) continue;

            if (bagGameItem.Id != previousBagItem.Id)
                bagSb.AppendValueLine(bagGameItem.Id, 100,
                                      bagGameItem.CreationDate, bagGameItem.CreatorId, bagGameItem.ModificationDate, bagGameItem.ModifierId, bagGameItem.Version);

            bagItemSb.AppendValueLine(bagGameItem.Id, bagGameItem.GameItemId, bagGameItem.Quantity,
                                      bagGameItem.CreationDate, bagGameItem.CreatorId, bagGameItem.ModificationDate, bagGameItem.ModifierId, bagGameItem.Version);

            previousBagItem = bagGameItem;
        }

        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(Bag)}.sql", bagSb.ToString());
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(BagGameItem)}.sql", bagItemSb.ToString());
    }
}