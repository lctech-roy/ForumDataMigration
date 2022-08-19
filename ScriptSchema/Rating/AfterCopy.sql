ALTER TABLE "ArticleRating"
    ADD CONSTRAINT "PK_ArticleRating" PRIMARY KEY ("Id"),
    ADD CONSTRAINT "FK_ArticleRating_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") ON DELETE CASCADE;

ALTER TABLE "ArticleRatingItem"
    ADD CONSTRAINT "PK_ArticleRatingItem" PRIMARY KEY ("Id", "CreditId"),
    ADD CONSTRAINT "FK_ArticleRatingItem_ArticleRating_Id" FOREIGN KEY ("Id") REFERENCES "ArticleRating" ("Id") ON DELETE CASCADE;

CREATE UNIQUE INDEX "IX_ArticleRating_ArticleId_CreatorId" ON "ArticleRating" ("ArticleId", "CreatorId");
