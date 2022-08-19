using Lctech.Jkf.Domain.Entities;
using Lctech.Jkf.Domain.Enums;
using Netcorext.Algorithms;
using Netcorext.EntityFramework.UserIdentityPattern;
using Dapper;
using ForumDataMigration.Extensions;
using ForumDataMigration.Models;
using MySql.Data.MySqlClient;


namespace ForumDataMigration;

public class ArticleMigration
{
    private readonly ISnowflake _snowflake;

    public ArticleMigration(ISnowflake snowflake)
    {
        _snowflake = snowflake;
    }

    public void Migration()
    {
        using var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION);

        const string readSql = @"SELECT tid, MAX(uid) AS ReadUid, MAX(pid) AS ReadFloor FROM pre_forum_read WHERE pid <> -1 GROUP BY tid";
        var readDic = cn.Query<Read>(readSql).ToDictionary(row=> row.Tid, row => row);

        for (var i = 15; i <= 15; i++)
        {
            var sql = $@"SELECT thread.tid AS Tid, 
                                    thread.displayorder , thread.special , thread.subject ,  
                                    thread.closed, thread.views , thread.replies,  
                                    thread.lastpost, thread.dateline, thread.sharetimes,  
                                    thread.typeid, thread.authorid, thread.highlight, 
                                    thread.digest, thread.readperm,
                                    COALESCE(postMain.pid,postBackUp.pid) AS Pid,
                                    COALESCE(postMain.message,postBackUp.message) AS message, 
                                    COALESCE(postMain.ratetimes,postBackUp.ratetimes) AS ratetimes,
                                    COALESCE(postMain.useip,postBackUp.useip) AS useip,
                                    COALESCE(postMain.usesig,postBackUp.usesig) AS usesig,
                                    thankCount.count,
                                    postDelay.post_time,
                                    warning.pid AS {nameof(ThreadWarning)}_Pid,
                                    warning.authorid AS {nameof(ThreadWarning)}_authorid,
                                    warning.operatorid AS {nameof(ThreadWarning)}_operatorid,
                                    warning.reason AS {nameof(ThreadWarning)}_reason,
                                    warning.dateline AS {nameof(ThreadWarning)}_dateline,
                                    rateLog.tid AS {nameof(ForumThread.RateLogs)}_Tid,
                                    rateLog.pid AS {nameof(ForumThread.RateLogs)}_Pid,
                                    rateLog.uid AS {nameof(ForumThread.RateLogs)}_Uid,
                                    rateLog.extcredits AS {nameof(ForumThread.RateLogs)}_Extcredits,
                                    rateLog.dateline AS {nameof(ForumThread.RateLogs)}_dateline,
                                    rateLog.score AS {nameof(ForumThread.RateLogs)}_score,
                                    rateLog.reason AS {nameof(ForumThread.RateLogs)}_reason,
                                    rateLog.forceshow AS {nameof(ForumThread.RateLogs)}_forceshow,
                                    pool.tid  AS {nameof(Poll)}_Tid,
                                    pool.voters AS {nameof(Poll)}_voters,
                                    pool.maxchoices AS {nameof(Poll)}_maxchoices,
                                    pool.overt AS {nameof(Poll)}_overt,
                                    pool.visible AS {nameof(Poll)}_visible,
                                    pool.expiration AS {nameof(Poll)}_expiration,
                                    poolOption.tid AS {nameof(Poll)}_{nameof(Poll.PollOptions)}_Tid,
                                    poolOption.polloptionid AS {nameof(Poll)}_{nameof(Poll.PollOptions)}_polloptionid,
                                    poolOption.votes AS {nameof(Poll)}_{nameof(Poll.PollOptions)}_votes,
                                    poolOption.polloption AS {nameof(Poll)}_{nameof(Poll.PollOptions)}_polloption,
                                    poolVoter.tid AS {nameof(Poll)}_{nameof(Poll.PollVoters)}_Tid,
                                    poolVoter.uid AS {nameof(Poll)}_{nameof(Poll.PollVoters)}_uid,
                                    poolVoter.options AS {nameof(Poll)}_{nameof(Poll.PollVoters)}_options,
                                    poolVoter.dateline AS {nameof(Poll)}_{nameof(Poll.PollVoters)}_dateline
                                    FROM (
                                      SELECT *
                                      FROM `pre_forum_thread`
                                      -- WHERE posttableid = {i}
                                      -- where tid = 14260173 or tid = 4347443 -- test Vote
                                      where tid = 5233129 -- test Rate
                                      LIMIT @Limit OFFSET @Offset
                                    ) AS `thread`
                                    LEFT JOIN `pre_forum_post` AS `postBackUp` ON `thread`.`tid` = `postBackUp`.`tid` and postBackUp.position = 1
                                    LEFT JOIN `pre_forum_post{(i!=0 ? "_"+i : "")}` AS `postMain` ON `thread`.`tid` = `postMain`.`tid` and postMain.position = 1
                                    LEFT JOIN `pre_forum_thankcount` AS `thankCount` ON `thread`.`tid` = `thankCount`.`tid`
                                    LEFT JOIN `pre_post_delay` AS `postDelay` ON `thread`.`tid` = `postDelay`.`tid`
                                    left join pre_forum_warning as warning on {(i!=0 ? "postMain.pid = warning.pid or ":"")}postBackUp.pid = warning.pid
                                    left join pre_forum_ratelog as rateLog on {(i!=0 ? "postMain.pid = rateLog.pid or ":"")}postBackUp.pid = rateLog.pid
                                    left join pre_forum_poll as pool on thread.tid = pool.tid
                                    left join pre_forum_polloption as poolOption on pool.tid = poolOption.tid
                                    left join pre_forum_pollvoter as poolVoter on pool.tid = poolVoter.tid";

            var dynamicForumThreads = cn.Query<dynamic>(sql, new { Limit = 100, Offset = 0 }).ToList();
            var forumThreads = Slapper.AutoMapper.MapDynamic<ForumThread>(dynamicForumThreads, false).ToList();

            //if (forumThreads.Where(x => x.Warning != null).Any()) { }

            var colorDic = new Dictionary<int, string?>()
                           {
                               { 0, null }, { 1, "#EE1B2E" }, { 2, "#EE5023" }, { 3, "#996600" }, { 4, "#3C9D40" }, { 5, "#2897C5" }, { 6, "#2B65B7" }, { 7, "#8F2A90" }, { 8, "#EC1282" }
                           };
            
            var results =
                forumThreads.Select(x =>
                                    {
                                        var isScheduling = x.PostTime < x.Dateline;
                                        var createDate = DateTimeOffset.FromUnixTimeSeconds(x.Dateline);
                                        var id = x.Tid;
                                        var pid = x.Pid;
                                        var highlightInt = x.Highlight % 10; //只要取個位數
                                        var read = readDic.ContainsKey(id) ? readDic[id] : null;

                                        var article = new Article()
                                                      {
                                                          Id = id,
                                                          CategoryId = x.Typeid,
                                                          Status = x.Displayorder == -1 ? ArticleStatus.Deleted :
                                                                   x.Displayorder == -2 ? ArticleStatus.Pending :
                                                                   x.Displayorder == -3 ? ArticleStatus.Hide : //待確認
                                                                   x.Displayorder == -4 ? ArticleStatus.Hide :
                                                                   isScheduling ? ArticleStatus.Scheduling :
                                                                   ArticleStatus.Published,
                                                          VisibleType = isScheduling ? VisibleType.Private : VisibleType.Public,

                                                          Type = x.Special switch
                                                                 {
                                                                     1 => ArticleType.Vote,
                                                                     2 => ArticleType.Diversion,
                                                                     3 => ArticleType.Reward,
                                                                     _ => ArticleType.Article
                                                                 },

                                                          PinType = x.Displayorder switch
                                                                    {
                                                                        1 => PinType.Board,
                                                                        2 => PinType.Area,
                                                                        3 => PinType.Global,
                                                                        _ => PinType.None
                                                                    },
                                                          Title = x.Subject ?? string.Empty,
                                                          Content = x.Message ?? string.Empty,
                                                          ViewCount = x.Views,
                                                          ReplyCount = x.Replies,
                                                          SortingIndex = x.Dateline,
                                                          LastReplyDate = x.Lastpost.HasValue ? DateTimeOffset.FromUnixTimeSeconds(x.Lastpost.Value) : null,
                                                          CreatorId = x.Authorid,
                                                          ModifierId = x.Authorid,
                                                          CreationDate = createDate,
                                                          ModificationDate = createDate,

                                                          // Detail = new ArticleDetail()
                                                          //          {
                                                          //              Id = id,
                                                          //              RatingCount = x.Ratetimes ?? 0,
                                                          //              ShareCount = x.Sharetimes,
                                                          //              Highlight = x.Highlight != 0,
                                                          //              HighlightColor = colorDic.ContainsKey(highlightInt) ? colorDic[highlightInt] : null,
                                                          //              Recommend = x.Digest,
                                                          //              ReadPermission = x.Readperm,
                                                          //              CommentDisabled = x.Closed == 1,
                                                          //              //LikeCount = t.t.t.Favtimes,
                                                          //              LikeCount = x.ThankCount ?? 0,
                                                          //              Ip = x.Useip,
                                                          //              Price = x.Price,
                                                          //              AuditorId = read?.ReadUid,
                                                          //              AuditFloor =read?.ReadFloor,
                                                          //              SchedulePublishDate = x.PostTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(x.PostTime.Value) : null,
                                                          //              Signature = x.Usesig,
                                                          //              Warning = x.Warning != null,
                                                          //              CreatorId = x.Authorid,
                                                          //              ModifierId = x.Authorid,
                                                          //              CreationDate = createDate,
                                                          //              ModificationDate = createDate,
                                                          //          },

                                                          Vote = x.Poll != null
                                                                     ? new ArticleVote
                                                                       {
                                                                           Id = id,
                                                                           CreationDate = createDate,
                                                                           CreatorId = x.Authorid,
                                                                           ModificationDate = createDate,
                                                                           ModifierId = x.Authorid,
                                                                           LimitNumberOfVotes = x.Poll.Maxchoices,
                                                                           Voters = x.Poll.Voters,
                                                                           PublicResult = x.Poll.Visible,
                                                                           PublicVoter = x.Poll.Overt,
                                                                           Deadline = DateTimeOffset.FromUnixTimeSeconds(x.Poll.Expiration),
                                                                           Items = x.Poll.PollOptions.Select(y => new ArticleVoteItem
                                                                                                                  {
                                                                                                                      Id = y.Polloptionid,
                                                                                                                      CreationDate = createDate,
                                                                                                                      CreatorId = x.Authorid,
                                                                                                                      ModificationDate = createDate,
                                                                                                                      ArticleVoteId = id,
                                                                                                                      Name = y.Polloption ?? string.Empty,
                                                                                                                      Votes = y.Votes,
                                                                                                                  }).ToArray()
                                                                       }
                                                                     : null,
                                                      };

                                        article.SetContentTypeAndCount();
                                        //article.Vote?.Items.SetVoteItemHistory(x.Poll!.PollVoters, _snowflake);
                                        article.Ratings.SetRating(x.RateLogs,_snowflake,id);
                                        

                                        var result = new ArticleResult()
                                                     {
                                                         Article = article,
                                                         Comment = new Comment
                                                                   {
                                                                       Id = id,
                                                                       RootId = id,
                                                                       Level = 0,
                                                                       Hierarchy = id.ToString(),
                                                                       Content = x.Message ?? string.Empty,
                                                                       VisibleType = VisibleType.Public,
                                                                       Ip = x.Useip,
                                                                       CreationDate = createDate,
                                                                       CreatorId = x.Authorid,
                                                                       ModificationDate = createDate,
                                                                   },
                                                         Warning = x.Warning != null
                                                                       ? new Warning
                                                                         {
                                                                             Id = _snowflake.Generate(),
                                                                             CreationDate = DateTimeOffset.FromUnixTimeSeconds(x.Warning.Dateline),
                                                                             CreatorId = x.Warning.Operatorid,
                                                                             ModificationDate = default,
                                                                             WarningType = WarningType.Article,
                                                                             SourceId = id,
                                                                             MemberId = x.Warning.Authorid,
                                                                             WarnerId = x.Warning.Operatorid,
                                                                             Reason = x.Warning.Reason
                                                                         }
                                                                       : null
                                                     };

                                        return result;
                                    }).ToArray();
        }

        Console.Write("111");
    }
}

// using (var conn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION_DEV))
//                                 {
//                                     var command = new MySqlCommand(sql,conn);
//                                     command.CommandText = sql;
//                                     command.Parameters.AddWithValue("@Start", period.StartSeconds);
//                                     command.Parameters.AddWithValue("@End", period.EndSeconds);
//                                     
//                                     conn.Open();
//
//                                     using (command)
//                                     {
//                                         var reader = command.ExecuteReader();
//
//                                         while (reader.Read())
//                                         {
//                                             if(reader.IsDBNull(0))
//                                                continue;
//                                             
//                                             posts.Add(new Post
//                                                       {
//                                                           Tid = reader.GetInt32(0),
//                                                           Displayorder = reader.GetInt16(1),
//                                                           Special = reader.GetInt16(2),
//                                                           Subject = reader.GetString(3),
//                                                           Closed = reader.GetUInt32(4),
//                                                           Views = reader.GetUInt32(5),
//                                                           Replies = reader.GetUInt32(6),
//                                                           Lastpost = reader.GetUInt32(7),
//                                                           Dateline = reader.GetUInt32(8),
//                                                           Sharetimes = reader.GetUInt32(9),
//                                                           Typeid = reader.GetUInt16(10),
//                                                           Authorid = reader.GetUInt32(11),
//                                                           Highlight = reader.GetInt16(12),
//                                                           Digest = reader.GetBoolean(13),
//                                                           Readperm = reader.GetByte(14),
//                                                           Cover = reader.GetString(15),
//                                                           Pid = reader.GetUInt32(16),
//                                                           Fid = reader.GetInt32(17),
//                                                           Message = reader.GetString(18),
//                                                           Ratetimes = reader.GetUInt32(19),
//                                                           Useip = reader.GetString(20),
//                                                           Usesig = reader.GetBoolean(21),
//                                                           Position = reader.GetUInt32(22),
//                                                           Tags = reader.GetString(23),
//                                                           Status = reader.GetUInt16(24),
//                                                           ThankCount = reader.IsDBNull(25) ? null : reader.GetInt32(25),
//                                                           PostTime =  reader.IsDBNull(26) ? null : reader.GetUInt32(26),
//
//                                                           // Price = 0,
//                                                           // ReadFloor = 0,
//                                                           // ReadUid = 0,
//                                                           // Warning = null
//                                                       });
//                                         }
//                                         reader.Close();
//                                     }
//                                 }


        //
        //
        // for (var i = 0; i <= 150; i++)
        // {
        //     var articlePath = $"{Setting.INSERT_DATA_PATH}/{nameof(Article)}/{i}";
        //     Directory.CreateDirectory(articlePath);
        //
        //     var articleDetailPath = $"{Setting.INSERT_DATA_PATH}/{nameof(ArticleDetail)}/{i}";
        //     Directory.CreateDirectory(articleDetailPath);
        //
        //     var warningPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Warning)}/{i}";
        //     Directory.CreateDirectory(warningPath);
        //
        //     var commentPath = $"{Setting.INSERT_DATA_PATH}/{nameof(Comment)}/{i}";
        //     Directory.CreateDirectory(commentPath);
        //
        //     var sql = $@"SELECT thread.tid AS Tid, 
        //                             thread.displayorder , thread.special , thread.subject ,  
        //                             thread.closed, thread.views , thread.replies,  
        //                             thread.lastpost, thread.dateline, thread.sharetimes,  
        //                             thread.typeid, thread.authorid, thread.highlight, 
        //                             thread.digest, thread.readperm,thread.cover,
        //                             post.pid AS Pid,
        //                             post.fid AS Fid,
        //                             post.message,
        //                             post.ratetimes,
        //                             post.useip,
        //                             post.usesig,
        //                             post.position,
        //                             post.tags,
        //                             post.status
        //                             -- thankCount.count AS thankCount,
        //                             -- postDelay.post_time AS postTime,
        //                             -- warning.pid AS {nameof(ThreadWarning)}_Pid,
        //                             -- warning.authorid AS {nameof(ThreadWarning)}_authorid,
        //                             -- warning.operatorid AS {nameof(ThreadWarning)}_operatorid,
        //                             -- warning.reason AS {nameof(ThreadWarning)}_reason,
        //                             -- warning.dateline AS {nameof(ThreadWarning)}_dateline
        //                             FROM (
        //                               SELECT tid,pid,fid,message,ratetimes,useip,usesig,position,tags,status
        //                                 FROM `pre_forum_post{(i != 0 ? $"_{i}" : "")}`
        //                                 where dateline >= @Start AND dateline < @End AND position = 1 AND first = true
        //                              -- FROM pre_forum_post where tid = 8967236 AND position = 1
        //                             ) AS post
        //                             LEFT JOIN pre_forum_thread AS thread ON thread.tid = post.tid
        //                             -- LEFT JOIN pre_forum_thankcount AS thankCount ON thankCount.tid = post.tid
        //                             -- LEFT JOIN pre_post_delay AS postDelay ON postDelay.tid = thread.tid
        //                             -- LEFT JOIN pre_forum_warning as warning on warning.pid = post.pid
        //                             ";
        //
        //     Parallel.ForEach(periods,
        //                      period =>
        //                      {
        //                          List<Post> posts;
        //
        //                          using (var cn = new MySqlConnection(Setting.OLD_FORUM_CONNECTION_DEV))
        //                          {
        //                              var dynamicPosts = cn.Query<dynamic>(sql, new { Start = period.StartSeconds, End = period.EndSeconds }).ToList();
        //                              posts = Slapper.AutoMapper.MapDynamic<Post>(dynamicPosts, false).ToList();
        //                          }
        //
        //                          if (!posts.Any())
        //                              return;
        //
        //                          var articleSb = new StringBuilder();
        //                          var articleDetailSb = new StringBuilder();
        //                          var commentSb = new StringBuilder();
        //                          var warningSb = new StringBuilder();
        //
        //
        //                          foreach (var post in posts)
        //                          {
        //                              //髒資料放過他
        //                              if (!articleDic.ContainsKey(post.Tid) || !boardDic.ContainsKey(post.Fid) || !categoryDic.ContainsKey(post.Typeid))
        //                                  continue;
        //
        //                              var articleId = articleDic[post.Tid];
        //                              var boardId = boardDic[post.Fid];
        //
        //                              var createDate = DateTimeOffset.FromUnixTimeSeconds(post.Dateline);
        //                              Comment comment;
        //
        //                              //文章
        //                              if (post.Position == 1)
        //                              {
        //                                  var isScheduling = post.PostTime < post.Dateline;
        //                                  var pid = post.Pid;
        //                                  var highlightInt = post.Highlight % 10; //只要取個位數
        //                                  var read = readDic.ContainsKey(post.Tid) ? readDic[post.Tid] : null;
        //                                  var imageCount = Setting.BbCodeImageRegex.Matches(post.Message).Count;
        //                                  var videoCount = Setting.BbCodeVideoRegex.Matches(post.Message).Count;
        //
        //                                  var article = new Article()
        //                                                {
        //                                                    Id = articleId,
        //                                                    Status = post.Displayorder == -1 ? ArticleStatus.Deleted :
        //                                                             post.Displayorder == -2 ? ArticleStatus.Pending :
        //                                                             post.Displayorder == -3 ? ArticleStatus.Hide : //待確認
        //                                                             post.Displayorder == -4 ? ArticleStatus.Hide :
        //                                                             isScheduling ? ArticleStatus.Scheduling :
        //                                                             ArticleStatus.Published,
        //                                                    Type = post.Special switch
        //                                                           {
        //                                                               1 => ArticleType.Vote,
        //                                                               2 => ArticleType.Diversion,
        //                                                               3 => ArticleType.Reward,
        //                                                               _ => ArticleType.Article
        //                                                           },
        //                                                    ContentType = imageCount switch
        //                                                                  {
        //                                                                      0 when videoCount == 0 => ContentType.PaintText,
        //                                                                      > 0 when videoCount > 0 => ContentType.Complex,
        //                                                                      _ => imageCount > 0 ? ContentType.Image : ContentType.Video
        //                                                                  },
        //                                                    PinType = post.Displayorder switch
        //                                                              {
        //                                                                  1 => PinType.Board,
        //                                                                  2 => PinType.Area,
        //                                                                  3 => PinType.Global,
        //                                                                  _ => PinType.None
        //                                                              },
        //                                                    VisibleType = isScheduling ? VisibleType.Private : VisibleType.Public,
        //                                                    Title = post.Subject,
        //                                                    Content = post.Message,
        //                                                    ViewCount = post.Views,
        //                                                    ReplyCount = post.Replies,
        //                                                    BoardId = boardId,
        //                                                    CategoryId = categoryDic[post.Typeid],
        //                                                    SortingIndex = post.Dateline,
        //                                                    LastReplyDate = post.Lastpost.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Lastpost.Value) : null,
        //                                                    LastReplierId = 0,
        //
        //                                                    //LastReplierId = post.laspo.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.Lastpost.Value) : null,,//To Do
        //                                                    PinPriority = 0,
        //                                                    CreatorId = post.Authorid,
        //                                                    ModifierId = post.Authorid,
        //                                                    CreationDate = createDate,
        //                                                    ModificationDate = createDate
        //                                                };
        //
        //                                  articleSb.Append($"{article.Id}{Setting.D}{article.BoardId}{Setting.D}{article.CategoryId}{Setting.D}{(int) article.Status}{Setting.D}{(int) article.VisibleType}{Setting.D}" +
        //                                                   $"{(int) article.Type}{Setting.D}{(int) article.ContentType}{Setting.D}{(int) article.PinType}{Setting.D}{article.Title.ToCopyText()}{Setting.D}" +
        //                                                   $"{article.Content.ToCopyText()}{Setting.D}{article.ViewCount}{Setting.D}{article.ReplyCount}{Setting.D}{article.SortingIndex}{Setting.D}{article.LastReplyDate.ToCopyValue()}{Setting.D}" +
        //                                                   $"{article.LastReplierId}{Setting.D}{article.PinPriority}{Setting.D}" +
        //                                                   $"{article.CreationDate}{Setting.D}{article.CreatorId}{Setting.D}{article.ModificationDate}{Setting.D}{article.ModifierId}{Setting.D}{article.Version}\n");
        //
        //
        //                                  var articleDetail = new ArticleDetail()
        //                                                      {
        //                                                          Id = articleId,
        //                                                          Cover = 0, //To Do post.cover
        //                                                          Tag = post.Tags.ToNewTags(),
        //                                                          RatingCount = post.Ratetimes ?? 0,
        //                                                          ShareCount = post.Sharetimes,
        //                                                          ImageCount = imageCount,
        //                                                          VideoCount = videoCount,
        //                                                          DonatePoint = 0,
        //                                                          Highlight = post.Highlight != 0,
        //                                                          HighlightColor = Setting.ColorDic.ContainsKey(highlightInt) ? Setting.ColorDic[highlightInt] : null,
        //                                                          Recommend = post.Digest,
        //                                                          ReadPermission = post.Readperm,
        //                                                          CommentDisabled = post.Closed == 1,
        //                                                          CommentVisibleType = post.Status == 34 ? VisibleType.Private : VisibleType.Public,
        //                                                          LikeCount = post.ThankCount ?? 0,
        //                                                          Ip = post.Useip,
        //                                                          Price = post.Price,
        //                                                          AuditorId = read?.ReadUid,
        //                                                          AuditFloor = read?.ReadFloor,
        //                                                          SchedulePublishDate = post.PostTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(post.PostTime.Value) : null,
        //                                                          HideExpirationDate = Setting.BbCodeHideTagRegex.IsMatch(post.Message!) ? createDate.AddDays(7) : null,
        //                                                          PinExpirationDate = modDic.ContainsKey((post.Tid, "EST")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "EST")]) : null,
        //                                                          RecommendExpirationDate = modDic.ContainsKey((post.Tid, "EDI")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "EDI")]) : null,
        //                                                          HighlightExpirationDate = modDic.ContainsKey((post.Tid, "EHL")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "EHL")]) : null,
        //                                                          CommentDisabledExpirationDate = modDic.ContainsKey((post.Tid, "ECL")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "ECL")]) : null,
        //                                                          InVisibleArticleExpirationDate = modDic.ContainsKey((post.Tid, "BNP")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "BNP")]) :
        //                                                                                           modDic.ContainsKey((post.Tid, "UBN")) ? DateTimeOffset.FromUnixTimeSeconds(modDic[(post.Tid, "UBN")]) : null,
        //                                                          Signature = post.Usesig,
        //                                                          Warning = post.Warning != null,
        //                                                          CreatorId = post.Authorid,
        //                                                          ModifierId = post.Authorid,
        //                                                          CreationDate = createDate,
        //                                                          ModificationDate = createDate,
        //                                                      };
        //
        //                                  articleDetailSb.Append($"{articleDetail.Id}{Setting.D}{articleDetail.Cover}{Setting.D}{articleDetail.Tag}{Setting.D}{articleDetail.RatingCount}{Setting.D}{articleDetail.ShareCount}{Setting.D}" +
        //                                                         $"{articleDetail.ImageCount}{Setting.D}{articleDetail.VideoCount}{Setting.D}{articleDetail.DonatePoint}{Setting.D}{articleDetail.Highlight}{Setting.D}{articleDetail.HighlightColor.ToCopyValue()}{Setting.D}" +
        //                                                         $"{articleDetail.Recommend}{Setting.D}{articleDetail.ReadPermission}{Setting.D}{articleDetail.CommentDisabled}{Setting.D}{(int) articleDetail.CommentVisibleType}{Setting.D}{articleDetail.LikeCount}{Setting.D}" +
        //                                                         $"{articleDetail.Ip}{Setting.D}{articleDetail.Price}{Setting.D}{articleDetail.AuditorId.ToCopyValue()}{Setting.D}{articleDetail.AuditFloor.ToCopyValue()}{Setting.D}" +
        //                                                         $"{articleDetail.SchedulePublishDate.ToCopyValue()}{Setting.D}{articleDetail.HideExpirationDate.ToCopyValue()}{Setting.D}{articleDetail.PinExpirationDate.ToCopyValue()}{Setting.D}" +
        //                                                         $"{articleDetail.RecommendExpirationDate.ToCopyValue()}{Setting.D}{articleDetail.HighlightExpirationDate.ToCopyValue()}{Setting.D}{articleDetail.CommentDisabledExpirationDate.ToCopyValue()}{Setting.D}" +
        //                                                         $"{articleDetail.InVisibleArticleExpirationDate.ToCopyValue()}{Setting.D}{articleDetail.Signature}{Setting.D}{articleDetail.Warning}{Setting.D}" +
        //                                                         $"{articleDetail.CreationDate}{Setting.D}{articleDetail.CreatorId}{Setting.D}{articleDetail.ModificationDate}{Setting.D}{articleDetail.ModifierId}{Setting.D}{articleDetail.Version}\n");
        //
        //                                  continue;
        //
        //                                  if (post.Warning != null)
        //                                  {
        //                                      var warningDate = DateTimeOffset.FromUnixTimeSeconds(post.Warning.Dateline);
        //
        //                                      var warning = new Warning
        //                                                    {
        //                                                        Id = _snowflake.Generate(),
        //                                                        WarningType = WarningType.Article,
        //                                                        SourceId = articleId,
        //                                                        MemberId = post.Warning.Authorid,
        //                                                        WarnerId = post.Warning.Operatorid,
        //                                                        Reason = post.Warning.Reason,
        //                                                        CreationDate = warningDate,
        //                                                        ModificationDate = warningDate,
        //                                                        CreatorId = post.Warning.Operatorid,
        //                                                    };
        //
        //                                      warningSb.Append($"{warning.Id}{Setting.D}{(int) warning.WarningType}{Setting.D}{warning.SourceId}{Setting.D}{warning.MemberId}{Setting.D}{warning.WarnerId}{Setting.D}{warning.Reason}{Setting.D}" +
        //                                                       $"{warning.CreationDate}{Setting.D}{warning.CreatorId}{Setting.D}{warning.ModificationDate}{Setting.D}{warning.ModifierId}{Setting.D}{warning.Version}\n");
        //                                  }
        //
        //                                  comment = new Comment
        //                                            {
        //                                                Id = articleId,
        //                                                RootId = articleId,
        //                                                BoardId = boardId,
        //                                                Level = 0,
        //                                                Hierarchy = articleId.ToString(),
        //                                                Content = post.Message,
        //                                                VisibleType = VisibleType.Public,
        //                                                Ip = post.Useip,
        //                                                CreationDate = createDate,
        //                                                CreatorId = post.Authorid,
        //                                                ModificationDate = createDate,
        //                                            };
        //                              }
        //                              else //留言
        //                              {
        //                                  comment = new Comment
        //                                            {
        //                                                Id = _snowflake.Generate(),
        //                                                RootId = articleId,
        //                                                BoardId = boardId,
        //                                                Level = (int) post.Position - 1,
        //                                                Hierarchy = articleId.ToString(),
        //                                                Content = post.Message,
        //                                                VisibleType = VisibleType.Public,
        //                                                Ip = post.Useip,
        //                                                CreationDate = createDate,
        //                                                CreatorId = post.Authorid,
        //                                                ModificationDate = createDate,
        //                                            };
        //                              }
        //
        //                              commentSb.Append($"{comment.Id}{Setting.D}{comment.RootId}{Setting.D}{comment.BoardId}{Setting.D}{comment.Level}{Setting.D}{comment.Hierarchy}{Setting.D}" +
        //                                               $"{comment.Content.ToCopyText()}{Setting.D}{(int) comment.VisibleType}{Setting.D}{comment.Ip}{Setting.D}" +
        //                                               $"{comment.CreationDate}{Setting.D}{comment.CreatorId}{Setting.D}{comment.ModificationDate}{Setting.D}{comment.ModifierId}{Setting.D}{comment.Version}\n");
        //                          }
        //
        //                          if (articleSb.Length > 0)
        //                          {
        //                              var fullPath = $"{articlePath}/{period.FileName}";
        //                              File.WriteAllText(fullPath, string.Concat(articleSql, articleSb.ToString()));
        //                              Console.WriteLine(fullPath);
        //                          }
        //
        //                          if (articleDetailSb.Length > 0)
        //                          {
        //                              var fullPath = $"{articleDetailPath}/{period.FileName}";
        //                              File.WriteAllText(fullPath, string.Concat(articleDetailSql, articleDetailSb.ToString()));
        //                              Console.WriteLine(fullPath);
        //                          }
        //
        //                          if (warningSb.Length > 0)
        //                          {
        //                              var fullPath = $"{warningPath}/{period.FileName}";
        //                              File.WriteAllText(fullPath, string.Concat(warningSql, warningSb.ToString()));
        //                              Console.WriteLine(fullPath);
        //                          }
        //
        //                          if (commentSb.Length > 0)
        //                          {
        //                              var fullPath = $"{commentPath}/{period.FileName}";
        //                              File.WriteAllText(fullPath, string.Concat(commentSql, commentSb.ToString()));
        //                              Console.WriteLine(fullPath);
        //                          }
        //                      });
        // }