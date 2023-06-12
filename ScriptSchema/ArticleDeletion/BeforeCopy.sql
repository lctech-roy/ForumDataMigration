CREATE TABLE "public"."ArticleDeletion" (
                                            "Id" int8 NOT NULL,
                                            "DeleterId" int8 NOT NULL,
                                            "DeletionDate" timestamptz NOT NULL,
                                            "DeletionReason" varchar(300) NOT NULL,
                                            PRIMARY KEY ("Id")
);