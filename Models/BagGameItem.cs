using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class BagGameItem : Entity
{
    public long GameItemId { get; set; }
    public int Quantity { get; set; }
}