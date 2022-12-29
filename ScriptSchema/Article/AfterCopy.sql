do $$
DECLARE beginDate timestamptz := '2007-01-01';
DECLARE tableName varchar;
begin
WHILE EXTRACT(YEAR FROM beginDate ) <= EXTRACT(Year FROM NOW() ) + 1 LOOP
    tableName = 'Article_' || EXTRACT(YEAR FROM beginDate );
    EXECUTE 'ALTER TABLE "' || tableName || '" ADD PRIMARY KEY ("Id");';
    beginDate = beginDate + interval '1 year';
END loop;
-- CREATE INDEX "IX_Article_PublishDate" ON "Article" ("PublishDate");
-- CREATE INDEX "IX_Article_BoardId" ON "Article" ("BoardId");
-- CREATE INDEX "IX_Article_CategoryId" ON "Article" ("CategoryId");
end$$;

ALTER TABLE "Article" SET LOGGED;
    
--刪除重複的資料
-- DELETE FROM "Article"
-- WHERE ctid IN (SELECT MIN(ctid) as ctid
--                FROM "Article"
--                GROUP BY "Id"
--                HAVING COUNT(*) > 1);

-- ALTER TABLE "ArticleReward"
--     ADD CONSTRAINT "PK_ArticleReward" PRIMARY KEY ("Id");


-- 連載 Table seed Data
-- TRUNCATE "ArticleSerialize";
-- INSERT INTO "ArticleSerialize" ("Id", "Status", "CreationDate", "ModificationDate", "Version", "CreatorId", "ModifierId")
-- SELECT
--     "Id",2 AS "Status",
--     "CreationDate",
--     "CreationDate" AS "ModificationDate",
--     0 AS "Version","CreatorId",
--     "CreatorId" AS "ModifierId"
-- FROM "Article"
-- WHERE "BoardId" IN (243402579247336,243402579247386)


