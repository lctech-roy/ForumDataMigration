ALTER TABLE "Attachment"
    ADD CONSTRAINT "PK_Attachment" PRIMARY KEY ("Id");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Attachment_Bucket_StoragePath_Name" ON "Attachment" ("Bucket", "StoragePath", "Name");

ALTER TABLE "AttachmentExtendData"
    SET LOGGED;
ALTER TABLE "Attachment"
    SET LOGGED;

ANALYZE "AttachmentExtendData";
ANALYZE "Attachment";