ALTER TABLE "ArticleVote"
    ADD CONSTRAINT "PK_ArticleVote" PRIMARY KEY ("Id");

ALTER TABLE "ArticleVoteItem"
    ADD CONSTRAINT "PK_ArticleVoteItem" PRIMARY KEY ("Id"),
    ADD CONSTRAINT "FK_ArticleVoteItem_ArticleVote_ArticleVoteId" FOREIGN KEY ("ArticleVoteId") REFERENCES "ArticleVote" ("Id") ON
        DELETE
        CASCADE;

ALTER TABLE "ArticleVoteItemHistory"
    ADD CONSTRAINT "PK_ArticleVoteItemHistory" PRIMARY KEY ("Id"),
    ADD CONSTRAINT "FK_ArticleVoteItemHistory_ArticleVoteItem_ArticleVoteItemId" FOREIGN KEY ("ArticleVoteItemId") REFERENCES "ArticleVoteItem" ("Id") ON
        DELETE
        CASCADE;

-- CREATE INDEX "IX_ArticleVoteItem_ArticleVoteId" ON "ArticleVoteItem" ("ArticleVoteId");
-- CREATE INDEX "IX_ArticleVoteItemHistory_ArticleVoteItemId" ON "ArticleVoteItemHistory" ("ArticleVoteItemId");

ALTER TABLE "ArticleVote"
    SET LOGGED;
ALTER TABLE "ArticleVoteItem"
    SET LOGGED;
ALTER TABLE "ArticleVoteItemHistory"
    SET LOGGED;

ANALYZE "ArticleVote";
ANALYZE "ArticleVoteItem";
ANALYZE "ArticleVoteItemHistory";