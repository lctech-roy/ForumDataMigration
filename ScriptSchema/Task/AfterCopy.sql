ALTER TABLE "Task"
    SET LOGGED;
ALTER TABLE "TaskRelation"
    SET LOGGED;
ALTER TABLE "TaskReward"
    SET LOGGED;

ANALYZE "Task";
ANALYZE "TaskRelation";
ANALYZE "TaskReward";

