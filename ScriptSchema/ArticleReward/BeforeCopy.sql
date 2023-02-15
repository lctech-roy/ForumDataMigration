ALTER TABLE "ArticleReward"
    DROP CONSTRAINT IF EXISTS "PK_ArticleReward" CASCADE;

TRUNCATE "ArticleReward";

ALTER TABLE "ArticleReward"
    SET UNLOGGED;

