ALTER TABLE "Comment"
    DROP CONSTRAINT IF EXISTS "PK_Comment" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_Comment_Comment_ParentId" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_Comment_Comment_RootId" CASCADE;

ALTER TABLE "CommentExtendData"
    DROP CONSTRAINT IF EXISTS "PK_CommentExtendData" CASCADE,
    DROP CONSTRAINT IF EXISTS "FK_CommentExtendData_Comment_Id" CASCADE;

DROP INDEX IF EXISTS "IX_Comment_ParentId";
DROP INDEX IF EXISTS "IX_Comment_RootId";
DROP INDEX IF EXISTS "IX_CommentExtendData_Key";
DROP INDEX IF EXISTS "IX_CommentExtendData_Value";

TRUNCATE "Comment";
TRUNCATE "CommentExtendData";