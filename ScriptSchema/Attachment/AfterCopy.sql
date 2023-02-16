ALTER TABLE "Attachment" ADD CONSTRAINT "PK_Attachment" PRIMARY KEY ("Id");

CREATE INDEX "IX_Attachment_CreationDate" ON "Attachment" ("CreationDate");

ALTER TABLE "AttachmentExtendData" SET LOGGED;
ALTER TABLE "Attachment" SET LOGGED;

ANALYZE "AttachmentExtendData";
ANALYZE "Attachment";