
-- 先補my sql索引
-- CREATE INDEX migrate_index ON pre_forum_thread (dateline, posttableid,displayorder);

ALTER TABLE "Article"
    DROP CONSTRAINT IF EXISTS "PK_Article" CASCADE;
ALTER TABLE "Article"
    DROP CONSTRAINT IF EXISTS "FK_Article_Board_BoardId";
ALTER TABLE "Article"
    DROP CONSTRAINT IF EXISTS "FK_Article_ArticleCategory_CategoryId";
ALTER TABLE "ArticleAttachment"
    DROP CONSTRAINT IF EXISTS "PK_ArticleAttachment";
DROP INDEX IF EXISTS "IX_ArticleAttachment_AttachmentId";

ALTER TABLE "Article"
    SET UNLOGGED;
ALTER TABLE "ArticleAttachment"
    SET UNLOGGED;

-- TRUNCATE "Article";
-- TRUNCATE "ArticleAttachment";


