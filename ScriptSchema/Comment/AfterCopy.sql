ALTER TABLE "Comment"
    ADD CONSTRAINT "PK_Comment" PRIMARY KEY ("Id");
ALTER TABLE "Comment"
    ADD CONSTRAINT "FK_Comment_Comment_ParentId" FOREIGN KEY ("ParentId") REFERENCES "Comment" ("Id") ON DELETE CASCADE;
ALTER TABLE "Comment"
    ADD CONSTRAINT "FK_Comment_Comment_RootId" FOREIGN KEY ("RootId") REFERENCES "Comment" ("Id") ON DELETE CASCADE;

ALTER TABLE "CommentExtendData"
    ADD CONSTRAINT "PK_CommentExtendData" PRIMARY KEY ("Id", "Key"),
    ADD CONSTRAINT "FK_CommentExtendData_Comment_Id" FOREIGN KEY ("Id") REFERENCES "Comment" ("Id") ON
        DELETE
        CASCADE;

ALTER TABLE "CommentAttachment"
    ADD CONSTRAINT "PK_CommentAttachment" PRIMARY KEY ("Id", "AttachmentId"),
    ADD CONSTRAINT "FK_CommentAttachment_Comment_Id" FOREIGN KEY ("Id") REFERENCES "public"."Comment" ("Id") ON DELETE CASCADE;

CREATE UNIQUE INDEX "IX_CommentAttachment_AttachmentId" ON "CommentAttachment" ("AttachmentId");

ALTER TABLE "Like"
    ADD CONSTRAINT "FK_Like_Comment_Id" FOREIGN KEY ("Id") REFERENCES "Comment" ("Id") ON DELETE CASCADE;

CREATE INDEX "IX_Comment_ParentId" ON "Comment" ("ParentId");
CREATE INDEX "IX_Comment_RootId" ON "Comment" ("RootId");
CREATE INDEX "IX_Comment_CreatorId" ON "Comment" ("CreatorId");
CREATE INDEX "IX_Comment_CreationDate" ON "Comment" ("CreationDate");

CREATE INDEX "IX_CommentExtendData_Key" ON "CommentExtendData" ("Key");
CREATE INDEX "IX_CommentExtendData_Value" ON "CommentExtendData" ("Value");

-- 更新連載資料
-- UPDATE "Comment" SET
--                      "Content" = regexp_replace("Content",'\s*',''),
--                      "Title" = SUBSTRING(
--                              COALESCE(
--                                      SUBSTRING("Content" FROM '([^\]]*)+?(?=\[\/)'),
--                                      SUBSTRING("Content" FROM '([^\s][[:punct:]\w \t]*)'))
--                              FOR 40)
-- WHERE "ParentId" IN (
--     SELECT "Id" FROM "CommentExtendData" WHERE "Key" = 'BoardId' AND "Value" IN  ('243402579247336','243402579247386'))

ALTER TABLE "Comment"
    SET LOGGED;
ALTER TABLE "CommentExtendData"
    SET LOGGED;
ALTER TABLE "CommentAttachment"
    SET LOGGED;

ANALYZE "Comment";
ANALYZE "CommentExtendData";
ANALYZE "CommentAttachment";


-- Create function 'update_comment_sequence'
CREATE OR REPLACE FUNCTION update_comment_sequence() RETURNS TRIGGER AS $$
BEGIN
    IF NEW."ParentId" IS NOT NULL THEN
        NEW."Sequence" = (SELECT (COALESCE((SELECT MAX("Sequence") FROM "Comment" WHERE "ParentId" = NEW."ParentId"), 0)) + 1);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger 'insert_comment'
CREATE OR REPLACE TRIGGER insert_comment
    BEFORE INSERT ON "Comment"
    FOR EACH ROW
EXECUTE PROCEDURE update_comment_sequence();