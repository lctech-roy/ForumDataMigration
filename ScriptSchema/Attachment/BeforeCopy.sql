ALTER TABLE "Attachment"
    DROP CONSTRAINT IF EXISTS "PK_Attachment" CASCADE;

ALTER TABLE "AttachmentExtendData"
    DROP CONSTRAINT IF EXISTS "PK_AttachmentExtendData" CASCADE;

DROP INDEX IF EXISTS "IX_Attachment_Bucket_StoragePath_Name";

DROP INDEX IF EXISTS "IX_Attachment_ProcessingState";
DROP INDEX IF EXISTS "IX_Attachment_ParentId";
DROP INDEX IF EXISTS "IX_Attachment_ModificationDate";
DROP INDEX IF EXISTS "IX_Attachment_Extension";
DROP INDEX IF EXISTS "IX_Attachment_DeleteStatus";
DROP INDEX IF EXISTS "IX_Attachment_CreationDate";
DROP INDEX IF EXISTS "IX_Attachment_ContentType";

DROP INDEX IF EXISTS "IX_AttachmentExtendData_Value";
DROP INDEX IF EXISTS "IX_AttachmentExtendData_Key";

ALTER TABLE "AttachmentExtendData"
    SET UNLOGGED;

ALTER TABLE "Attachment"
    SET UNLOGGED;


