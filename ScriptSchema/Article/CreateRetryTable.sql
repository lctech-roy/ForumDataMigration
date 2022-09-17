CREATE TABLE IF NOT EXISTS "ArticleRetry" (
                                              "FolderName" char(6) NULL,
                                              "FileName" varchar(20) NULL,
                                              "Exception" text
);

INSERT INTO "ArticleRetry" ("FolderName","FileName","Exception")
SELECT NULL,NULL,NULL
WHERE NOT EXISTS (SELECT FROM "ArticleRetry");