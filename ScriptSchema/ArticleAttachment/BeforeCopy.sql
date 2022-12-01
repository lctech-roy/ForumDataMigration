ALTER TABLE "ArticleAttachment"
    DROP CONSTRAINT IF EXISTS "PK_ArticleAttachment";
        
DROP INDEX IF EXISTS "IX_ArticleAttachment_AttachmentId";

TRUNCATE "ArticleAttachment";