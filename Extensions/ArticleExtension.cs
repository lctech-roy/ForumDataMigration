using System.Text.RegularExpressions;
using ForumDataMigration.Models;
using Lctech.Jkf.Domain.Entities;
using Lctech.Jkf.Domain.Enums;
using Netcorext.Algorithms;

namespace ForumDataMigration.Extensions;

public static class ArticleExtension
{
    private const string IMAGE_PATTERN = @"\[(?:img|attachimg)](.*?)\[\/(?:img|attachimg)]";
    private const string VIDEO_PATTERN = @"\[(media[^\]]*|video)](.*?)\[\/(media|video)]";

    private static readonly Regex BbCodeImageRegex = new(IMAGE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BbCodeVideoRegex = new(VIDEO_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void SetContentTypeAndCount(this Article article)
    {
        var imageCount = BbCodeImageRegex.Matches(article.Content).Count;
        var videoCount = BbCodeVideoRegex.Matches(article.Content).Count;

        var contentType = imageCount switch
                          {
                              0 when videoCount == 0 => ContentType.PaintText,
                              > 0 when videoCount > 0 => ContentType.Complex,
                              _ => imageCount > 0 ? ContentType.Image : ContentType.Video
                          };

        article.ContentType = contentType;
        article.ImageCount = imageCount;
        article.VideoCount = videoCount;
    }

    public static void SetVoteItemHistory(this ICollection<ArticleVoteItem> items, IEnumerable<PollVoter> pollVoters, ISnowflake snowflake,Dictionary<int, long> memberUidDic)
    {
        foreach (var pollVoter in pollVoters)
        {
            var creationDate = DateTimeOffset.FromUnixTimeSeconds(pollVoter.Dateline);

            foreach (var item in items)
            {
                if (pollVoter.Options.Contains(item.Id.ToString()))
                    item.Histories.Add(new ArticleVoteItemHistory
                                       {
                                           Id = snowflake.Generate(),
                                           ArticleVoteItemId = item.Id,
                                           CreationDate = creationDate,
                                           CreatorId = memberUidDic.ContainsKey(Convert.ToInt32(pollVoter.Uid)) ? memberUidDic[Convert.ToInt32(pollVoter.Uid)] : 0,
                                           ModificationDate = creationDate,
                                           ModifierId = 0,
                                       });
            }
        }
    }

    public static void SetRating(this ICollection<ArticleRating> articleRatings, IEnumerable<RateLog> rateLogs, ISnowflake snowflake, long id)
    {
        var ratingDic = new Dictionary<(uint pid, int tid, uint uid), ArticleRating>();

        foreach (var rateLog in rateLogs)
        {
            var rateCreateDate = DateTimeOffset.FromUnixTimeSeconds(rateLog.Dateline);
            var ratingId = snowflake.Generate();
            
            if (!ratingDic.ContainsKey((rateLog.Pid, rateLog.Tid, rateLog.Uid)))
            {
                var articleRating = new ArticleRating()
                                    {
                                        Id = ratingId,
                                        CreationDate = rateCreateDate,
                                        CreatorId = rateLog.Uid,
                                        ModificationDate = rateCreateDate,
                                        ModifierId = 0,
                                        ArticleId = id,
                                        VisibleType = VisibleType.Private,
                                        Content = rateLog.Reason,
                                        Items =
                                        {
                                            new ArticleRatingItem
                                            {
                                                Id = ratingId,
                                                CreationDate = rateCreateDate,
                                                CreatorId = rateLog.Uid,
                                                ModificationDate = rateCreateDate,
                                                ModifierId = 0,
                                                CreditId = rateLog.Extcredits,
                                                Point = rateLog.Score,
                                            }
                                        }
                                    };

                ratingDic.Add((rateLog.Pid, rateLog.Tid, rateLog.Uid), articleRating);
                articleRatings.Add(articleRating);
            }
            else
            {
                var existsRating = ratingDic[(rateLog.Pid, rateLog.Tid, rateLog.Uid)];

                existsRating.Items.Add(new ArticleRatingItem
                                       {
                                           Id = ratingId,
                                           CreationDate = rateCreateDate,
                                           CreatorId = rateLog.Uid,
                                           ModificationDate = rateCreateDate,
                                           ModifierId = 0,
                                           CreditId = rateLog.Extcredits,
                                           Point = rateLog.Score,
                                       });
            }
        }
    }
}