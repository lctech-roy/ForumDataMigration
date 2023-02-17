ALTER TABLE "Bag"
    ADD CONSTRAINT "PK_Bag" PRIMARY KEY ("Id");

ALTER TABLE "BagGameItem"
    ADD CONSTRAINT "PK_BagGameItem" PRIMARY KEY ("Id", "GameItemId"),
    ADD CONSTRAINT "FK_BagGameItem_Bag_Id" FOREIGN KEY ("Id") REFERENCES "Bag" ("Id") ON
        DELETE
        CASCADE,
    ADD CONSTRAINT "FK_BagGameItem_GameItem_GameItemId" FOREIGN KEY ("GameItemId") REFERENCES "GameItem" ("Id") ON DELETE
        CASCADE;

CREATE INDEX "IX_BagGameItem_GameItemId" ON "BagGameItem" ("GameItemId");

ANALYZE "Bag";
ANALYZE "BagGameItem";
