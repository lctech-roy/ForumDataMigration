TRUNCATE "Task" CASCADE;

ALTER TABLE "Task"
    SET UNLOGGED;
ALTER TABLE "TaskRelation"
    SET UNLOGGED;
ALTER TABLE "TaskReward"
    SET UNLOGGED;
