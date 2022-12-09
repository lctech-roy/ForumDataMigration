ALTER TABLE "ArticleRatingItem"
    DROP CONSTRAINT IF EXISTS "PK_ArticleRatingItem",
    DROP CONSTRAINT IF EXISTS "FK_ArticleRatingItem_ArticleRating_Id";

ALTER TABLE "ArticleRating"
    DROP CONSTRAINT IF EXISTS "PK_ArticleRating",
    DROP CONSTRAINT IF EXISTS "FK_ArticleRating_Article_ArticleId";

-- DROP INDEX IF EXISTS "IX_ArticleRating_ArticleId_CreatorId";

TRUNCATE "ArticleRatingItem";
TRUNCATE "ArticleRating";

ALTER TABLE "ArticleRating" SET UNLOGGED;
ALTER TABLE "ArticleRatingItem" SET UNLOGGED;