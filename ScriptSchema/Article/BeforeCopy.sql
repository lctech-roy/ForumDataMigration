-- do
-- $$
--     DECLARE
--         beginDate         timestamptz := '2007-01-01';
--         DECLARE tableName varchar;
--     begin
--         WHILE EXTRACT(YEAR FROM beginDate) <= EXTRACT(Year FROM NOW()) + 1
--             LOOP
--                 tableName = 'Article_' || EXTRACT(YEAR FROM beginDate);
--                 EXECUTE 'ALTER TABLE "' || tableName || '" DROP CONSTRAINT IF EXISTS "' || tableName ||
--                         '_pkey" CASCADE;';
--                 beginDate = beginDate + interval '1 year';
--             END loop;
--     end
-- $$;

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


