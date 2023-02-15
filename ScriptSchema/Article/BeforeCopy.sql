do
$$
    DECLARE
        beginDate         timestamptz := '2007-01-01';
        DECLARE tableName varchar;
    begin
        WHILE EXTRACT(YEAR FROM beginDate) <= EXTRACT(Year FROM NOW()) + 1
            LOOP
                tableName = 'Article_' || EXTRACT(YEAR FROM beginDate);
                EXECUTE 'ALTER TABLE "' || tableName || '" DROP CONSTRAINT IF EXISTS "' || tableName ||
                        '_pkey" CASCADE;';
                beginDate = beginDate + interval '1 year';
            END loop;
    end
$$;

ALTER TABLE "ArticleReward"
    DROP CONSTRAINT IF EXISTS "PK_ArticleReward" CASCADE;

-- DROP INDEX IF EXISTS "IX_Article_PublishDate";
-- DROP INDEX IF EXISTS "IX_Article_BoardId";
-- DROP INDEX IF EXISTS "IX_Article_CategoryId";

ALTER TABLE "Article"
    SET UNLOGGED;


-- TRUNCATE "Article";


