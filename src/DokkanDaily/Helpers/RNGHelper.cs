using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;

namespace DokkanDaily.Helpers
{
    public static class RNGHelper
    {
        public static Random GetDailySeed()
        {
            var date = DateTime.UtcNow.Date;
            var seed = date.Year * 1000 + date.DayOfYear;
            return new Random(seed);
        }

        private static List<T> CreateSeededCollection<T>(IEnumerable<T> input, Tier tier) where T : ITieredObject
        {
            List<T> output = [];

            foreach(T item in input)
            {
                int diff = (int)tier - (int)item.Tier;
                if (diff < 2)
                {
                    diff = Math.Abs(diff);
                    for (int i = 0; i < 6 - diff; i++)
                    {
                        output.Add(item);
                    }
                }
            }

            return output;
        }

        public static DailyType GetRandomDailyType(Random random)
        {
            return DDConstants.DailyTypes[random.Next(0, DDConstants.DailyTypes.Count)];
        }

        public static LinkSkill GetRandomLinkSkill(Random random)
        {
            return DDConstants.LinkSkills[random.Next(0, DDConstants.LinkSkills.Count)];
        }

        public static LinkSkill GetRandomLinkSkill(Random random, Tier minTier)
        {
            var links = CreateSeededCollection(DDConstants.LinkSkills, minTier);
            return links[random.Next(0, links.Count)];
        }

        public static Event GetRandomStage(Random random)
        {
            return DDConstants.Events[random.Next(0, DDConstants.Events.Count)];
        }

        public static Category GetRandomCategory(Random random)
        {
            return DDConstants.Categories[random.Next(0, DDConstants.Categories.Count)];
        }
        public static Category GetRandomCategory(Random random, Tier minTier)
        {
            var cats = CreateSeededCollection(DDConstants.Categories, minTier);
            return cats[random.Next(0, cats.Count)];
        }

        public static Leader GetRandomLeader(Random random)
        {
            return DDConstants.Leaders[random.Next(0, DDConstants.Leaders.Count)];
        }

        public static Leader GetRandomLeader(Random random, Tier minTier)
        {
            var leaders = CreateSeededCollection(DDConstants.Leaders, minTier);
            return leaders[random.Next(0, leaders.Count)];
        }
    }
}