--刪除重複的資料
DELETE FROM "Article"
WHERE ctid IN (SELECT MIN(ctid) as ctid
               FROM "Article"
               GROUP BY "Id"
               HAVING COUNT(*) > 1);

--刪除CategoryId對應不到的資料
-- DELETE FROM "Article" WHERE "CategoryId" = 0;

ALTER TABLE "Article"
    ADD CONSTRAINT "FK_Article_Board_BoardId" FOREIGN KEY ("BoardId") REFERENCES "Board" ("Id") ON DELETE CASCADE,
    ADD CONSTRAINT "FK_Article_ArticleCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "ArticleCategory" ("Id") ON DELETE CASCADE;

ALTER TABLE "ArticleReward"
    ADD CONSTRAINT "PK_ArticleReward" PRIMARY KEY ("Id");

CREATE INDEX "IX_Article_BoardId" ON "Article" ("BoardId");
CREATE INDEX "IX_Article_CategoryId" ON "Article" ("CategoryId");