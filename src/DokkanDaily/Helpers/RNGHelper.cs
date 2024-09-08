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
    }
}