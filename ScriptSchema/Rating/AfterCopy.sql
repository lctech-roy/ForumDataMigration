ALTER TABLE "ArticleRating"
    ADD CONSTRAINT "PK_ArticleRating" PRIMARY KEY ("Id");
-- ADD CONSTRAINT "FK_ArticleRating_Article_ArticleId" FOREIGN KEY ("ArticleId") REFERENCES "Article" ("Id") ON DELETE CASCADE;

ALTER TABLE "ArticleRatingItem"
    ADD CONSTRAINT "PK_ArticleRatingItem" PRIMARY KEY ("Id", "CreditId"),
    ADD CONSTRAINT "FK_ArticleRatingItem_ArticleRating_Id" FOREIGN KEY ("Id") REFERENCES "ArticleRating" ("Id") ON DELETE CASCADE;

-- CREATE UNIQUE INDEX "IX_ArticleRating_ArticleId_CreatorId" ON "ArticleRating" ("ArticleId", "CreatorId");

-- ALTER TABLE "ArticleRating" SET LOGGED;
-- ALTER TABLE "ArticleRatingItem" SET LOGGED;

-- UPDATE "Article" a SET "RatingCount" = ar."ratecount"
-- FROM (SELECT "ArticleId", COUNT("ArticleId") AS ratecount FROM "ArticleRating" GROUP BY "ArticleId") ar
-- WHERE a."Id" IN(SELECT DISTINCT "ArticleId" FROM "ArticleRating") AND a."Id" = ar."ArticleId";