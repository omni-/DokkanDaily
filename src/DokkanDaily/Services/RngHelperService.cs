using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class RngHelperService(IOptions<DokkanDailySettings> settings) : IRngHelperService
    {
        private static readonly int MaxTierDifference = 2;
        private static readonly int MaxCopies = 6;

        private Random Random => GetDailySeed();
        private readonly DokkanDailySettings Settings = settings.Value;

        public Random GetDailySeed()
        {
            var date = DateTime.UtcNow.Date;
            var seed = (date.Year * 1000 + date.DayOfYear) + Settings.SeedOffset;
            return new Random(seed);
        }

        public DailyType GetRandomDailyType()
        {
            return DDConstants.DailyTypes[Random.Next(0, DDConstants.DailyTypes.Count)];
        }

        public LinkSkill GetRandomLinkSkill()
        {
            return DDConstants.LinkSkills[Random.Next(0, DDConstants.LinkSkills.Count)];
        }

        public LinkSkill GetRandomLinkSkill(Tier minTier)
        {
            var links = CreateSeededCollection(DDConstants.LinkSkills, minTier);
            return links[Random.Next(0, links.Count)];
        }

        public Event GetRandomStage()
        {
            return DDConstants.Events[Random.Next(0, DDConstants.Events.Count)];
        }

        public Category GetRandomCategory()
        {
            return DDConstants.Categories[Random.Next(0, DDConstants.Categories.Count)];
        }
        public Category GetRandomCategory(Tier minTier)
        {
            var cats = CreateSeededCollection(DDConstants.Categories, minTier);
            return cats[Random.Next(0, cats.Count)];
        }

        public Leader GetRandomLeader()
        {
            return DDConstants.Leaders[Random.Next(0, DDConstants.Leaders.Count)];
        }

        public Leader GetRandomLeader(Tier minTier)
        {
            var leaders = CreateSeededCollection(DDConstants.Leaders, minTier);
            return leaders[Random.Next(0, leaders.Count)];
        }

        private static List<T> CreateSeededCollection<T>(IEnumerable<T> input, Tier tier) where T : ITieredObject
        {
            List<T> output = [];

            foreach (T item in input)
            {
                int diff = (int)tier - (int)item.Tier;
                if (diff < MaxTierDifference)
                {
                    diff = Math.Abs(diff);
                    for (int i = 0; i < MaxCopies - diff; i++)
                    {
                        output.Add(item);
                    }
                }
            }

            return output;
        }

    }
}