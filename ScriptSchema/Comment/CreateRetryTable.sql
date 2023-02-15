CREATE TABLE IF NOT EXISTS "CommentRetry"
(
    "FolderName" char(6)     NULL,
    "FileName"   varchar(20) NULL,
    "Exception"  text
);

INSERT INTO "CommentRetry" ("FolderName", "FileName", "Exception")
SELECT NULL, NULL, NULL
WHERE NOT EXISTS(SELECT FROM "CommentRetry");