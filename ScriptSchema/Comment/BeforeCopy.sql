ALTER TABLE "Comment"
    DROP CONSTRAINT IF EXISTS "PK_Comment" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_Comment_Comment_ParentId" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_Comment_Comment_RootId" CASCADE;

ALTER TABLE "CommentExtendData"
    DROP CONSTRAINT IF EXISTS "PK_CommentExtendData" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_CommentExtendData_Comment_Id" CASCADE;

ALTER TABLE "CommentAttachment"
    DROP CONSTRAINT IF EXISTS "PK_CommentAttachment";

DROP INDEX IF EXISTS "IX_CommentAttachment_AttachmentId";

DROP INDEX IF EXISTS "IX_Comment_ParentId";
DROP INDEX IF EXISTS "IX_Comment_RootId";
DROP INDEX IF EXISTS "IX_Comment_CreatorId";
DROP INDEX IF EXISTS "IX_Comment_CreationDate";

DROP INDEX IF EXISTS "IX_CommentExtendData_Key";
DROP INDEX IF EXISTS "IX_CommentExtendData_Value";

ALTER TABLE "Comment"
    SET UNLOGGED;
ALTER TABLE "CommentExtendData"
    SET UNLOGGED;
ALTER TABLE "CommentAttachment"
    SET UNLOGGED;
ALTER TABLE "Like"
    SET UNLOGGED;

DROP TRIGGER IF EXISTS insert_comment ON "Comment";

DROP FUNCTION IF EXISTS update_comment_sequence;

-- TRUNCATE "Comment";
-- TRUNCATE "CommentExtendData";
-- TRUNCATE "CommentAttachment";
