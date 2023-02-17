ALTER TABLE "MemberBag"
    ADD CONSTRAINT "PK_MemberBag" PRIMARY KEY ("Id");

ALTER TABLE "MemberBagItem"
    ADD CONSTRAINT "PK_MemberBagItem" PRIMARY KEY ("Id", "MedalId"),
    ADD CONSTRAINT "FK_MemberBagItem_Medal_MedalId" FOREIGN KEY ("MedalId") REFERENCES "Medal" ("Id") ON
        DELETE
        CASCADE,
    ADD CONSTRAINT "FK_MemberBagItem_MemberBag_Id" FOREIGN KEY ("Id") REFERENCES "MemberBag" ("Id") ON DELETE
        CASCADE;

ANALYZE "MemberBag";
ANALYZE "MemberBagItem";