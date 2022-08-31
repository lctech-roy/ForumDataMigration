using Netcorext.EntityFramework.UserIdentityPattern.Entities;

namespace ForumDataMigration.Models;

public class Bag : Entity
{
    public int Limit { get; set; }
}