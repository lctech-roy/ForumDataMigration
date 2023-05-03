using System.Text;
using ForumDataMigration.Extensions;
using ForumDataMigration.Helpers;
using Lctech.TaskCenter.Domain.Entities;
using Lctech.TaskCenter.Models.Enums;
using Netcorext.Algorithms;
using Netcorext.Extensions.Linq;

using Task = Lctech.TaskCenter.Domain.Entities.Task;

namespace ForumDataMigration;

public class TaskMigration
{
    private const string TASK_SQL = $"COPY \"{nameof(Task)}\" " +
                                   $"(\"{nameof(Task.Id)}\",\"{nameof(Task.Name)}\",\"{nameof(Task.Description)}\",\"{nameof(Task.Source)}\",\"{nameof(Task.RequiredPoint)}\"" +
                                   $",\"{nameof(Task.OriginGroupId)}\",\"{nameof(Task.OriginGroup)}\",\"{nameof(Task.IsOriginGroup)}\",\"{nameof(Task.IsDelete)}\"" +
                                   $",\"{nameof(Task.IsHidden)}\",\"{nameof(Task.Type)}\",\"{nameof(Task.Level)}\",\"{nameof(Task.SubLevel)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private const string TASK_EXTEND_DATA_SQL = $"COPY \"{nameof(TaskExtendData)}\" " +
                                           $"(\"{nameof(TaskExtendData.Id)}\",\"{nameof(TaskExtendData.Key)}\",\"{nameof(TaskExtendData.Value)}\"" + Setting.COPY_ENTITY_SUFFIX;
    
    private const string TASK_RELATION_SQL = $"COPY \"{nameof(TaskRelation)}\" " +
                                             $"(\"{nameof(TaskRelation.Id)}\",\"{nameof(TaskRelation.SubTaskId)}\",\"{nameof(TaskRelation.RelationType)}\",\"{nameof(TaskRelation.Source)}\"" +
                                             $",\"{nameof(TaskRelation.GroupId)}\",\"{nameof(TaskRelation.Group)}\",\"{nameof(TaskRelation.IsHidden)}\",\"{nameof(TaskRelation.IsDelete)}\"" +
                                             $",\"{nameof(TaskRelation.SortingIndex)}\"" + Setting.COPY_ENTITY_SUFFIX;
    
    private const string TASK_REWARD_SQL = $"COPY \"{nameof(TaskReward)}\" " +
                                             $"(\"{nameof(TaskReward.Id)}\",\"{nameof(TaskReward.RewardId)}\",\"{nameof(TaskReward.RewardType)}\",\"{nameof(TaskReward.Count)}\"" + Setting.COPY_ENTITY_SUFFIX;

    private readonly ISnowflake _snowflake;

    public TaskMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        var settings = AchievementHelper.GetAchievementSettings().ToArray();
        
        var tasks = new List<Task>();
        
        foreach (var setting in settings)
        {
            var requiredPoint = 0;
            long? previousId = null;
            var sortingIndex = 0;
            
            var groupId =  _snowflake.Generate();
            
            var originGroupTask = new Task
                                  {
                                      Id = groupId,
                                      Type = TaskType.Achievement,
                                      Source = setting.Source,
                                      OriginGroup = setting.Group,
                                      IsOriginGroup = true,
                                      OriginGroupId = groupId,
                                      Name = setting.Name,
                                      Description = setting.Description,
                                      RequiredPoint = null
                                  };
            
            tasks.Add(originGroupTask);
            
            setting.LayerSettings
                   .ForEach(x =>
                            {
                                for (var i = 0; i < x.SubLevel; i++)
                                {
                                    var id = _snowflake.Generate();
                                    
                                    var task = new Task()
                                               {
                                                   Id = id,
                                                   Type = TaskType.Achievement,
                                                   Source = setting.Source,
                                                   OriginGroup = setting.Group,
                                                   OriginGroupId = groupId,
                                                   Level = x.Level,
                                                   SubLevel = i,
                                                   Name = setting.Name,
                                                   Description = setting.Description,
                                                   RequiredPoint = requiredPoint,
                                                   SubTaskRelations = previousId.HasValue
                                                                          ? new List<TaskRelation>
                                                                            {
                                                                                new()
                                                                                {
                                                                                    Id = previousId.Value,
                                                                                    SubTaskId = id,
                                                                                    RelationType = RelationType.Child,
                                                                                    Source = setting.Source,
                                                                                    Group = setting.Group,
                                                                                    GroupId = groupId,
                                                                                    SortingIndex = ++sortingIndex,
                                                                                }
                                                                            }
                                                                          : new List<TaskRelation>()
                                               };

                                    if (!previousId.HasValue)
                                    {
                                        originGroupTask.TaskRelations.Add(new TaskRelation
                                                                          {
                                                                              Id = groupId,
                                                                              SubTaskId = id,
                                                                              RelationType = RelationType.Child,
                                                                              Source = setting.Source,
                                                                              Group = setting.Group,
                                                                              GroupId = groupId,
                                                                              SortingIndex = 0,
                                                                          });
                                    }
                                    
                                    if (previousId.HasValue)
                                        task.TaskRewards.Add(new TaskReward
                                                             {
                                                                 Id = id,
                                                                 RewardId = 0,
                                                                 RewardType = RewardType.Achievement,
                                                                 Count = 10,
                                                             });

                                    if (i == 0)
                                    {
                                        if (x.RewardSetting != null)
                                            task.TaskRewards.Add(new TaskReward
                                                                 {
                                                                     Id = id,
                                                                     RewardId = x.RewardSetting.RewardId,
                                                                     RewardType = x.RewardSetting.RewardType,
                                                                     Count = x.RewardSetting.Count,
                                                                 });

                                        task.ExtendData.Add(new TaskExtendData()
                                                            {
                                                                Id = id,
                                                                Key = nameof(x.SubLevel) + "Count",
                                                                Value = x.SubLevel.ToString()
                                                            });

                                        task.ExtendData.Add(new TaskExtendData()
                                                            {
                                                                Id = id,
                                                                Key = nameof(x.Interval),
                                                                Value = x.Interval.ToString()
                                                            });
                                    }

                                    tasks.Add(task);

                                    requiredPoint += x.Interval;
                                    previousId = task.Id;
                                }
                            });
        }
        
        var taskSb = new StringBuilder(TASK_SQL);
        var taskExtendDataSb = new StringBuilder(TASK_EXTEND_DATA_SQL);
        var taskRelationSb = new StringBuilder(TASK_RELATION_SQL);
        var taskRewardSb = new StringBuilder(TASK_REWARD_SQL);
        var dateNow = DateTimeOffset.UtcNow;
        
        foreach (var task in tasks)
        {
            taskSb.AppendValueLine(task.Id, task.Name.ToCopyValue(),task.Description.ToCopyValue(),task.Source, task.RequiredPoint.ToCopyValue(),
                                   task.OriginGroupId, task.OriginGroup, task.IsOriginGroup, task.IsDelete,
                                   task.IsHidden,(int)task.Type,task.Level.ToCopyValue(),task.SubLevel.ToCopyValue(),
                                   dateNow, task.CreatorId, dateNow, task.ModifierId, task.Version);

            foreach (var taskExtendData in task.ExtendData)
            {
                taskExtendDataSb.AppendValueLine(task.Id,taskExtendData.Key,taskExtendData.Value,
                                                 dateNow, taskExtendData.CreatorId, dateNow, taskExtendData.ModifierId, taskExtendData.Version);
            }
            
            foreach (var taskRelation in task.TaskRelations)
            {
                taskRelationSb.AppendValueLine(taskRelation.Id,taskRelation.SubTaskId,(int)taskRelation.RelationType,taskRelation.Source,
                                               taskRelation.GroupId,taskRelation.Group,taskRelation.IsHidden,taskRelation.IsDelete,taskRelation.SortingIndex,
                                               dateNow, taskRelation.CreatorId, dateNow, taskRelation.ModifierId, taskRelation.Version);
            }
            
            foreach (var subTaskRelation in task.SubTaskRelations)
            {
                taskRelationSb.AppendValueLine(subTaskRelation.Id,subTaskRelation.SubTaskId,(int)subTaskRelation.RelationType,subTaskRelation.Source,
                                               subTaskRelation.GroupId,subTaskRelation.Group,subTaskRelation.IsHidden,subTaskRelation.IsDelete,subTaskRelation.SortingIndex,
                                               dateNow, subTaskRelation.CreatorId, dateNow, subTaskRelation.ModifierId, subTaskRelation.Version);
            }
            
            foreach (var taskReward in task.TaskRewards)
            {
                taskRewardSb.AppendValueLine(task.Id,taskReward.RewardId,(int)taskReward.RewardType,taskReward.Count,
                                             dateNow, taskReward.CreatorId, dateNow, taskReward.ModifierId, taskReward.Version);
            }
        }

        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(Task)}.sql", taskSb.ToString());
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(TaskExtendData)}.sql", taskExtendDataSb.ToString());
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(TaskRelation)}.sql", taskRelationSb.ToString());
        File.WriteAllText($"{Setting.INSERT_DATA_PATH}/{nameof(TaskReward)}.sql", taskRewardSb.ToString());
    }
}