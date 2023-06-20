ALTER TABLE "Attachment"
    ADD CONSTRAINT "PK_Attachment" PRIMARY KEY ("Id");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Attachment_Bucket_StoragePath_Name" ON "Attachment" ("Bucket", "StoragePath", "Name");

ALTER TABLE "AttachmentExtendData"
    ADD CONSTRAINT "PK_AttachmentExtendData" PRIMARY KEY ("Id","Key");

CREATE INDEX IF NOT EXISTS "IX_Attachment_ModificationDate" ON "Attachment" ("ModificationDate");

CREATE INDEX IF NOT EXISTS "IX_AttachmentExtendData_Key" ON "AttachmentExtendData" ("Key");
CREATE INDEX IF NOT EXISTS "IX_AttachmentExtendData_Value" ON "AttachmentExtendData" ("Value");

ALTER TABLE "AttachmentExtendData"
    SET LOGGED;
ALTER TABLE "Attachment"
    SET LOGGED;

ANALYZE "AttachmentExtendData";
ANALYZE "Attachment";