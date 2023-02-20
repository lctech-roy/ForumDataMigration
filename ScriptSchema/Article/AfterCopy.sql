do
$$
    DECLARE
        beginDate         timestamptz := '2007-01-01';
        DECLARE tableName varchar;
    begin
        WHILE EXTRACT(YEAR FROM beginDate) <= EXTRACT(Year FROM NOW()) + 1
            LOOP
                tableName = 'Article_' || EXTRACT(YEAR FROM beginDate);
                EXECUTE 'ALTER TABLE "' || tableName || '" ADD PRIMARY KEY ("Id");';
                beginDate = beginDate + interval '1 year';
            END loop;
    end
$$;

ALTER TABLE "Article"
    ADD CONSTRAINT "FK_Article_Board_BoardId" FOREIGN KEY ("BoardId") REFERENCES "public"."Board" ("Id");
ALTER TABLE "Article"
    ADD CONSTRAINT "FK_Article_ArticleCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "public"."ArticleCategory" ("Id");
ALTER TABLE "ArticleAttachment"
    ADD CONSTRAINT "PK_ArticleAttachment" PRIMARY KEY ("Id", "AttachmentId");

CREATE UNIQUE INDEX "IX_ArticleAttachment_AttachmentId" ON "ArticleAttachment" ("AttachmentId");

ALTER TABLE "Article"
    SET LOGGED;
ALTER TABLE "ArticleAttachment"
    SET LOGGED;

ANALYZE "Article";
ANALYZE "ArticleAttachment";

--刪除重複的資料
-- DELETE FROM "Article"
-- WHERE ctid IN (SELECT MIN(ctid) as ctid
--                FROM "Article"
--                GROUP BY "Id"
--                HAVING COUNT(*) > 1);

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


