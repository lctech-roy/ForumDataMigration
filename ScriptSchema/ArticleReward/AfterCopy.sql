ALTER TABLE "ArticleReward"
    ADD CONSTRAINT "PK_ArticleReward" PRIMARY KEY ("Id");

ALTER TABLE "ArticleReward" SET LOGGED;

ANALYZE "ArticleReward";
