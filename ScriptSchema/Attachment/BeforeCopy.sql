ALTER TABLE "Attachment"
    DROP CONSTRAINT IF EXISTS "PK_Attachment" CASCADE;

-- DROP INDEX IF EXISTS "IX_Attachment_CreationDate";


ALTER TABLE "AttachmentExtendData" SET UNLOGGED;

ALTER TABLE "Attachment" SET UNLOGGED;


