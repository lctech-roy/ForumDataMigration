ALTER TABLE "Bag"
    DROP CONSTRAINT IF EXISTS "PK_Bag" CASCADE;

ALTER TABLE "BagGameItem"
    DROP CONSTRAINT IF EXISTS "PK_BagGameItem" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_BagGameItem_Bag_Id" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_BagGameItem_GameItem_GameItemId" CASCADE;
        
DROP INDEX IF EXISTS "IX_BagGameItem_GameItemId";

TRUNCATE "Bag";
TRUNCATE "BagGameItem";