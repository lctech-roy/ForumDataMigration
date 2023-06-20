ALTER TABLE "Attachment"
    DROP CONSTRAINT IF EXISTS "PK_Attachment" CASCADE;

ALTER TABLE "AttachmentExtendData"
    DROP CONSTRAINT IF EXISTS "PK_AttachmentExtendData" CASCADE;

DROP INDEX IF EXISTS "IX_Comment_KeywordModificationDate_Level";

DROP INDEX IF EXISTS "IX_Attachment_Bucket_StoragePath_Name";

DROP INDEX IF EXISTS "IX_AttachmentExtendData_Value";

DROP INDEX IF EXISTS "IX_AttachmentExtendData_Key";

ALTER TABLE "AttachmentExtendData"
    SET UNLOGGED;

ALTER TABLE "Attachment"
    SET UNLOGGED;


