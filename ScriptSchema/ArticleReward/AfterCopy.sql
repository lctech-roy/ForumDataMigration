ALTER TABLE "ArticleReward"
    ADD CONSTRAINT "PK_ArticleReward" PRIMARY KEY ("Id");

ALTER TABLE "ArticleReward"
    SET LOGGED;

ANALYZE "ArticleReward";

UPDATE "Article" SET "Price" = 0 WHERE "Type" = 3;
