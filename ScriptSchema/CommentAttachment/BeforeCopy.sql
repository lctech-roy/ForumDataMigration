ALTER TABLE "CommentAttachment"
DROP
CONSTRAINT IF EXISTS "PK_CommentAttachment";
        
-- DROP INDEX IF EXISTS "IX_CommentAttachment_AttachmentId";

-- TRUNCATE "CommentAttachment";

ALTER TABLE "CommentAttachment" SET UNLOGGED;