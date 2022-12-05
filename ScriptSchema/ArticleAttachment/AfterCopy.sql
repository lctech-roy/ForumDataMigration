ALTER TABLE "ArticleAttachment"
    ADD CONSTRAINT "PK_ArticleAttachment" PRIMARY KEY ("Id","AttachmentId");

CREATE UNIQUE INDEX "IX_ArticleAttachment_AttachmentId" ON "ArticleAttachment" ("AttachmentId");

ALTER TABLE "ArticleAttachment" SET LOGGED;