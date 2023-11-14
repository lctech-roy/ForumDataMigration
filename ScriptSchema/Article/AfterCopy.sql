ALTER TABLE "Article"
    SET LOGGED;
ALTER TABLE "ArticleAttachment"
    SET LOGGED;

ALTER TABLE "Article"
    ADD CONSTRAINT "PK_Article" PRIMARY KEY ("Id");
ALTER TABLE "Article"
    ADD CONSTRAINT "FK_Article_Board_BoardId" FOREIGN KEY ("BoardId") REFERENCES "public"."Board" ("Id");
ALTER TABLE "Article"
    ADD CONSTRAINT "FK_Article_ArticleCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "public"."ArticleCategory" ("Id");

ALTER TABLE "ArticleAttachment"
    ADD CONSTRAINT "PK_ArticleAttachment" PRIMARY KEY ("Id", "AttachmentId");
CREATE UNIQUE INDEX "IX_ArticleAttachment_AttachmentId" ON "ArticleAttachment" ("AttachmentId");

--將Fk加回去
ALTER TABLE "ArticleCelebrity" ADD CONSTRAINT "FK_ArticleCelebrity_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleContact" ADD CONSTRAINT "FK_ArticleContact_Article_Id" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleDailyStatistics" ADD CONSTRAINT "FK_ArticleDailyStatistics_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleDiversion" ADD CONSTRAINT "FK_ArticleDiversion_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleLike" ADD CONSTRAINT "FK_ArticleLike_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleRating" ADD CONSTRAINT "FK_ArticleRating_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleReward" ADD CONSTRAINT "FK_ArticleReward_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id");
ALTER TABLE "ArticleTransaction" ADD CONSTRAINT "FK_ArticleTransaction_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleSerialize" ADD CONSTRAINT "FK_ArticleSerialize_Article_Id" FOREIGN KEY ("Id") REFERENCES "public"."Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleVote" ADD CONSTRAINT "FK_ArticleVote_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id");
ALTER TABLE "ArticleAttachment" ADD CONSTRAINT "FK_ArticleAttachment_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "OrderArticle" ADD CONSTRAINT "FK_OrderArticle_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") MATCH SIMPLE ON UPDATE NO ACTION ON DELETE CASCADE;
ALTER TABLE "PinArticleHistory" ADD CONSTRAINT "FK_PinArticleHistory_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id") MATCH SIMPLE ON UPDATE NO ACTION ON DELETE CASCADE;
ALTER TABLE "ArticleMassageReview" ADD CONSTRAINT "FK_ArticleMassage_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id");
ALTER TABLE "ArticleMassageReviewHistory" ADD CONSTRAINT "FK_ArticleMassageReviewHistory_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id");
ALTER TABLE "ArticleMassageCommentViolationHistory" ADD CONSTRAINT "FK_ArticleMassageCommentViolationHistory_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id");
ALTER TABLE "ArticleFile" ADD CONSTRAINT "FK_ArticleFile_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleMassageHistory" ADD CONSTRAINT "FK_ArticleMassageHistory_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") ON DELETE CASCADE;
ALTER TABLE "ArticleReward" ADD CONSTRAINT "FK_ArticleReward_Article_Id" FOREIGN KEY ("Id") REFERENCES "Article" ("Id");

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
-- WHERE "BoardId" IN (228,209)


