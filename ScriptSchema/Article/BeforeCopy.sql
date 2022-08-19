ALTER TABLE "Article"
    -- DROP CONSTRAINT IF EXISTS "PK_Article" CASCADE ,
    DROP CONSTRAINT IF EXISTS "FK_Article_ArticleCategory_CategoryId" CASCADE ,
    DROP CONSTRAINT IF EXISTS "FK_Article_Board_BoardId" CASCADE ;

ALTER TABLE "ArticleReward"
    DROP CONSTRAINT IF EXISTS "PK_ArticleReward" CASCADE;

DROP INDEX IF EXISTS "IX_Article_BoardId";
DROP INDEX IF EXISTS "IX_Article_CategoryId";

TRUNCATE "Article";

