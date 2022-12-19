-- Remove Duplicate
-- DELETE FROM "MedalRelation"
-- WHERE ctid IN (SELECT MIN(ctid) as ctid
--                FROM "MedalRelation"
--                GROUP BY "OriMedalId"
--                HAVING COUNT(*) > 1);

ALTER TABLE "MemberBag"
    DROP CONSTRAINT IF EXISTS "PK_MemberBag" CASCADE;

ALTER TABLE "MemberBagItem"
    DROP CONSTRAINT IF EXISTS "PK_MemberBagItem" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_MemberBagItem_Medal_MedalId" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_MemberBagItem_MemberBag_Id" CASCADE;

TRUNCATE "MemberBag";
TRUNCATE "MemberBagItem";