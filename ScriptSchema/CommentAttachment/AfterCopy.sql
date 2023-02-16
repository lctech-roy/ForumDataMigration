ALTER TABLE "CommentAttachment"
    ADD CONSTRAINT "PK_CommentAttachment" PRIMARY KEY ("Id", "AttachmentId");

CREATE UNIQUE INDEX "IX_CommentAttachment_AttachmentId" ON "CommentAttachment" ("AttachmentId");

ALTER TABLE "CommentAttachment" SET LOGGED;

ANALYZE "CommentAttachment";
