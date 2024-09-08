using DokkanDaily.Constants;
using DokkanDaily.Models;

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
            List<LinkSkill> links = DDConstants.LinkSkills.Where(x => x.Tier >= minTier).ToList();
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
            List<Category> cats = DDConstants.Categories.Where(x => x.Tier >= minTier).ToList();
            return cats[random.Next(0, cats.Count)];
        }

        public static Unit GetRandomLeader(Random random)
        {
            return DDConstants.Leaders[random.Next(0, DDConstants.Leaders.Count)];
        }

        public static Unit GetRandomLeader(Random random, Tier minTier)
        {
            List<Unit> leaders = DDConstants.Leaders.Where(x => x.Tier >= minTier).ToList();
            return leaders[random.Next(0, leaders.Count)];
        }
    }
}