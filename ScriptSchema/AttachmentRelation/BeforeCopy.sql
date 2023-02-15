DROP TABLE IF EXISTS "public"."ArticleRelation";

CREATE TABLE "public"."AttachmentRelation"
(
    "Pid" int4 NOT NULL,
    "Aid" int4 NOT NULL,
    "Id"  int8 NOT NULL
);

