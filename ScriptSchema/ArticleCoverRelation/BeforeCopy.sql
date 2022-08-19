DROP TABLE IF EXISTS "public"."ArticleCoverRelation";

CREATE TABLE "public"."ArticleCoverRelation"
(
    "Id"  int8         NOT NULL,
    "OriginCover" varchar(200) NOT NULL,
    "Tid"           int4         NOT NULL,
    "Pid"           int4         NOT NULL,
    "AttachmentUrl" varchar(1000) NOT NULL
);

