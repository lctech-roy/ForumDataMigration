using Lctech.TaskCenter.Models.Achievement;
using Lctech.TaskCenter.Models.Enums;

namespace ForumDataMigration.Helpers;

public static class AchievementHelper
{
    public static IEnumerable<AchievementSetting> GetAchievementSettings()
    {
        var settings = new List<AchievementSetting>
                       {
                           new()
                           {
                               Group = "HavingGold",
                               Name = "金幣",
                               Description = "擁有金幣",
                               LayerSettings = new (int interval, int subLevel)[]
                                               {
                                                   (5, 22),
                                                   (7, 78),
                                                   (10, 100),
                                                   (50, 100),
                                                   (100, 100),
                                                   (500, 100),
                                                   (1000, 100),
                                                   (1500, 100),
                                                   (2000, 100),
                                                   (5000, 100),
                                                   (10000, 100),
                                                   (100000, 100),
                                                   (500000, 100),
                                                   (1000000, 100)
                                               }.Select((layer, index) =>
                                                        {
                                                            var level = index + 1;

                                                            var layerSetting = new LayerSetting
                                                                               {
                                                                                   Level = level,
                                                                                   Interval = layer.interval,
                                                                                   SubLevel = layer.subLevel,
                                                                               };

                                                            layerSetting.RewardSetting = level switch
                                                                                         {
                                                                                             2 => new RewardSetting
                                                                                                  {
                                                                                                      RewardType = RewardType.Item,
                                                                                                      RewardId = 238034384192096, //勇氣之羽
                                                                                                      Count = 1
                                                                                                  },
                                                                                             3 => new RewardSetting
                                                                                                  {
                                                                                                      RewardType = RewardType.Item,
                                                                                                      RewardId = 238034379997292, //古代銀幣
                                                                                                      Count = 1
                                                                                                  },
                                                                                             4 => new RewardSetting
                                                                                                  {
                                                                                                      RewardType = RewardType.Item,
                                                                                                      RewardId = 238034384192096, //勇氣之羽
                                                                                                      Count = 2
                                                                                                  },
                                                                                             5 => new RewardSetting
                                                                                                  {
                                                                                                      RewardType = RewardType.Item,
                                                                                                      RewardId = 238034379997292, //古代銀幣
                                                                                                      Count = 2
                                                                                                  },
                                                                                             6 => new RewardSetting
                                                                                                  {
                                                                                                      RewardType = RewardType.Item,
                                                                                                      RewardId = 238034379997537, //中型體力藥水
                                                                                                      Count = 1
                                                                                                  },
                                                                                             _ => layerSetting.RewardSetting
                                                                                         };

                                                            return layerSetting;
                                                        }
                                                       ).ToArray()
                           },
                           new()
                           {
                               Group = "ObtainGold",
                               Name = "金幣",
                               Description = "獲得金幣",
                               LayerSettings = new (int interval, int subLevel)[]
                                               {
                                                   (5, 22),
                                                   (5, 78),
                                                   (10, 100),
                                                   (50, 100),
                                                   (100, 100),
                                                   (500, 100),
                                                   (1000, 100),
                                                   (1500, 100),
                                                   (2000, 100),
                                                   (5000, 100),
                                                   (10000, 100),
                                                   (100000, 100),
                                                   (500000, 100),
                                                   (1000000, 100),
                                               }.Select((layer, index) =>
                                                            new LayerSetting
                                                            {
                                                                Level = index + 1,
                                                                Interval = layer.interval,
                                                                SubLevel = layer.subLevel,
                                                            }).ToArray()
                           },
                           new()
                           {
                               Group = "ConsumeGold",
                               Name = "金幣",
                               Description = "消耗金幣",
                               LayerSettings = new (int interval, int subLevel)[]
                                               {
                                                   (2, 22),
                                                   (2, 78),
                                                   (10, 100),
                                                   (50, 100),
                                                   (100, 100),
                                                   (500, 100),
                                                   (1000, 100),
                                                   (1500, 100),
                                                   (2000, 100),
                                                   (5000, 100),
                                                   (10000, 100),
                                                   (50000, 100),
                                                   (100000, 100),
                                                   (1000000, 100),
                                               }.Select((layer, index) =>
                                                            new LayerSetting
                                                            {
                                                                Level = index + 1,
                                                                Interval = layer.interval,
                                                                SubLevel = layer.subLevel,
                                                            }).ToArray()
                           },
                           new()
                           {
                               Group = "HavingHeart",
                               Name = "愛心",
                               Description = "擁有愛心",
                               LayerSettings = new (int interval, int subLevel)[]
                                               {
                                                   (1, 22),
                                                   (1, 78),
                                                   (5, 100),
                                                   (10, 100),
                                                   (50, 100),
                                                   (100, 100),
                                                   (500, 100),
                                                   (1000, 100),
                                                   (1500, 100),
                                                   (2000, 100),
                                                   (5000, 100),
                                                   (10000, 100),
                                                   (100000, 100),
                                                   (500000, 100),
                                               }.Select((layer, index) =>
                                                            new LayerSetting
                                                            {
                                                                Level = index + 1,
                                                                Interval = layer.interval,
                                                                SubLevel = layer.subLevel,
                                                            }).ToArray()
                           },
                           new()
                           {
                               Group = "ObtainHeart",
                               Name = "愛心",
                               Description = "獲得愛心",
                               LayerSettings = new (int interval, int subLevel)[]
                                               {
                                                   (1, 22),
                                                   (1, 78),
                                                   (5, 100),
                                                   (10, 100),
                                                   (50, 100),
                                                   (100, 100),
                                                   (500, 100),
                                                   (1000, 100),
                                                   (1500, 100),
                                                   (2000, 100),
                                                   (5000, 100),
                                                   (10000, 100),
                                                   (100000, 100),
                                                   (500000, 100),
                                               }.Select((layer, index) =>
                                                            new LayerSetting
                                                            {
                                                                Level = index + 1,
                                                                Interval = layer.interval,
                                                                SubLevel = layer.subLevel,
                                                            }).ToArray()
                           },
                           new()
                           {
                               Group = "ConsumeHeart",
                               Name = "愛心",
                               Description = "消耗愛心",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (1, 22),
                                       (1, 78),
                                       (5, 100),
                                       (10, 100),
                                       (50, 100),
                                       (100, 100),
                                       (500, 100),
                                       (1000, 100),
                                       (1500, 100),
                                       (2000, 100),
                                       (5000, 100),
                                       (10000, 100),
                                       (100000, 100),
                                       (500000, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "GiveHeart",
                               Name = "送出愛心",
                               Description = "送出愛心次數",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (5, 22),
                                       (5, 78),
                                       (10, 100),
                                       (50, 100),
                                       (100, 100),
                                       (500, 100),
                                       (1000, 100),
                                       (1500, 100),
                                       (2000, 100),
                                       (5000, 100),
                                       (10000, 100),
                                       (100000, 100),
                                       (500000, 100),
                                       (1000000, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "HavingReputation",
                               Name = "名聲",
                               Description = "擁有名聲",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (5, 22),
                                       (5, 78),
                                       (10, 100),
                                       (50, 100),
                                       (100, 100),
                                       (500, 100),
                                       (1000, 100),
                                       (1500, 100),
                                       (2000, 100),
                                       (5000, 100),
                                       (10000, 100),
                                       (100000, 100),
                                       (500000, 100),
                                       (1000000, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "HavingStrength",
                               Name = "體力",
                               Description = "擁有體力",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (1, 5),
                                       (1, 16),
                                       (1, 29),
                                       (1, 50),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "ObtainStrength",
                               Name = "體力",
                               Description = "獲得體力",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (1, 22),
                                       (1, 78),
                                       (5, 100),
                                       (10, 100),
                                       (20, 100),
                                       (30, 100),
                                       (40, 100),
                                       (50, 100),
                                       (60, 100),
                                       (70, 100),
                                       (80, 100),
                                       (90, 100),
                                       (100, 100),
                                       (100, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "ConsumeStrength",
                               Name = "體力",
                               Description = "消耗體力",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (1, 22),
                                       (1, 78),
                                       (5, 100),
                                       (10, 100),
                                       (20, 100),
                                       (30, 100),
                                       (40, 100),
                                       (50, 100),
                                       (60, 100),
                                       (70, 100),
                                       (80, 100),
                                       (90, 100),
                                       (100, 100),
                                       (100, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "GiveInvitation",
                               Name = "邀請",
                               Description = "發送邀請",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (1, 5),
                                       (1, 16),
                                       (1, 29),
                                       (1, 50),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                       (1, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "ClickLike",
                               Name = "點讚留言",
                               Description = "點讚次數",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (5, 22),
                                       (5, 78),
                                       (10, 100),
                                       (50, 100),
                                       (100, 100),
                                       (500, 100),
                                       (1000, 100),
                                       (1500, 100),
                                       (2000, 100),
                                       (5000, 100),
                                       (10000, 100),
                                       (100000, 100),
                                       (500000, 100),
                                       (1000000, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "LikeClicked",
                               Name = "留言被點讚",
                               Description = "被點讚次數",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (5, 22),
                                       (5, 78),
                                       (10, 100),
                                       (50, 100),
                                       (100, 100),
                                       (500, 100),
                                       (1000, 100),
                                       (1500, 100),
                                       (2000, 100),
                                       (5000, 100),
                                       (10000, 100),
                                       (100000, 100),
                                       (500000, 100),
                                       (1000000, 100),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                           new()
                           {
                               Group = "TotalSign",
                               Name = "每日簽到",
                               Description = "累積總簽到數",
                               LayerSettings =
                                   new (int interval, int subLevel)[]
                                   {
                                       (1, 3),
                                       (1, 4),
                                       (1, 8),
                                       (1, 15),
                                       (1, 30),
                                       (1, 60),
                                       (1, 120),
                                       (1, 125),
                                       (1, 385),
                                       (1, 750),
                                       (1, 150),
                                       (1, 240),
                                       (1, 365),
                                       (1, 750),
                                       (1, 225),
                                       (1, 300),
                                       (1, 500),
                                       (1, 200),
                                       (1, 300),
                                       (1, 500),
                                       (1, 200),
                                       (1, 300),
                                       (1, 500),
                                       (1, 200),
                                       (1, 300),
                                   }.Select((layer, index) =>
                                                new LayerSetting
                                                {
                                                    Level = index + 1,
                                                    Interval = layer.interval,
                                                    SubLevel = layer.subLevel,
                                                }).ToArray()
                           },
                       };

        return settings;
    }
}