ALTER TABLE "ArticleVote"
DROP
CONSTRAINT IF EXISTS "PK_ArticleVote" CASCADE;

ALTER TABLE "ArticleVoteItem"
DROP
CONSTRAINT IF EXISTS "PK_ArticleVoteItem" CASCADE,
    DROP
CONSTRAINT IF EXISTS "FK_ArticleVoteItem_ArticleVote_ArticleVoteId" CASCADE;

ALTER TABLE "ArticleVoteItemHistory"
DROP
CONSTRAINT IF EXISTS "PK_ArticleVoteItemHistory" CASCADE,
    DROP
CONSTRAINT IF EXISTS "FK_ArticleVoteItemHistory_ArticleVoteItem_ArticleVoteItemId" CASCADE;
        
-- DROP INDEX IF EXISTS "IX_ArticleVoteItem_ArticleVoteId";
-- DROP INDEX IF EXISTS "IX_ArticleVoteItemHistory_ArticleVoteItemId";

TRUNCATE "ArticleVote";
TRUNCATE "ArticleVoteItem";
TRUNCATE "ArticleVoteItemHistory";

ALTER TABLE "ArticleVote" SET UNLOGGED;
ALTER TABLE "ArticleVoteItem" SET UNLOGGED;
ALTER TABLE "ArticleVoteItemHistory" SET UNLOGGED;