DROP TABLE IF EXISTS "public"."ExternalAttachmentUrl";

CREATE TABLE "public"."ExternalAttachmentUrl"
(
    "AttachmentId"  int8          NOT NULL,
    "Tid"           int4          NOT NULL,
    "Pid"           int4          NOT NULL,
    "AttachmentUrl" varchar(1000) NOT NULL
);

