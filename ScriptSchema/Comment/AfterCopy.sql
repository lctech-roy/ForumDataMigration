ALTER TABLE "Comment" ADD CONSTRAINT "PK_Comment" PRIMARY KEY ("Id");
ALTER TABLE "Comment" ADD CONSTRAINT "FK_Comment_Comment_ParentId" FOREIGN KEY ("ParentId") REFERENCES "Comment" ("Id") ON DELETE CASCADE;
ALTER TABLE "Comment" ADD CONSTRAINT "FK_Comment_Comment_RootId" FOREIGN KEY ("RootId") REFERENCES "Comment" ("Id") ON DELETE CASCADE;

ALTER TABLE "CommentExtendData"
    ADD CONSTRAINT "PK_CommentExtendData" PRIMARY KEY ("Id", "Key"),
    ADD CONSTRAINT "FK_CommentExtendData_Comment_Id" FOREIGN KEY ("Id") REFERENCES "Comment" ("Id") ON DELETE CASCADE;

ALTER TABLE "Like" ADD CONSTRAINT "FK_Like_Comment_Id" FOREIGN KEY ("Id") REFERENCES "Comment" ("Id") ON DELETE CASCADE;

CREATE INDEX "IX_Comment_ParentId" ON "Comment" ("ParentId");
CREATE INDEX "IX_Comment_RootId" ON "Comment" ("RootId");
CREATE INDEX "IX_CommentExtendData_Key" ON "CommentExtendData" ("Key");
CREATE INDEX "IX_CommentExtendData_Value" ON "CommentExtendData" ("Value");