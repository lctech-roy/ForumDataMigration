UPDATE "Article"
SET "DeleterId" = ad."DeleterId",
    "DeletionDate" = ad."DeletionDate",
    "DeletionReason" = ad."DeletionReason"
FROM "ArticleDeletion" ad
WHERE "Article"."Id" = ad."Id" AND "Article"."DeleteStatus" = 1;

