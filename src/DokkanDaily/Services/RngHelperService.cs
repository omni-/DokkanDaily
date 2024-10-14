using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services
{
    public class RngHelperService : IRngHelperService
    {
        private Random Random => GetDailySeed();

        private static int SeedOffset { get; set; }
        private static int? SeedOverride { get; set; }
        public static Challenge ChallengeOverride { get; private set; }
        public static DailyType? DailyTypeOverride { get; private set; }

        public Random GetDailySeed()
        {
            return new Random(GetRawSeed());
        }

        public void OverrideChallenge(DailyType type, Event e, LinkSkill link, Category cat, Leader l)
        {
            ChallengeOverride = new(type, e, link, cat, l, DokkanDailyHelper.GetUnit(l));
        }

        public void OverrideChallengeType(DailyType type)
        {
            DailyTypeOverride = type;
        }

        public void Reset()
        {
            DailyTypeOverride = null;
            ChallengeOverride = null;
            SeedOverride = null;
            SeedOffset = 0;
        }

        public void SetDailySeed(int seed)
        {
            SeedOverride = seed;
        }

        public void RollDailySeed()
        {
            SeedOverride = null;
            SeedOffset++;
        }

        public int GetRawSeed()
        {
            if (SeedOverride != null) return SeedOverride.Value;

            var date = DateTime.UtcNow.Date;
            var seed = (date.Year * 1000 + date.DayOfYear) / (SeedOffset + 1);
            return seed;
        }

        public DailyType GetRandomDailyType()
        {
            if (DailyTypeOverride != null) return DailyTypeOverride.Value;

            // link skill challenges are harder and less varied, so they should appear slightly less often
            var types = new List<DailyType>(DokkanConstants.DailyTypes);
            types.Remove(DailyType.LinkSkill);
            types = [.. types.Concat(new List<DailyType>(DokkanConstants.DailyTypes))];
            return types[Random.Next(0, types.Count)];
        }

        public LinkSkill GetRandomLinkSkill()
        {
            return DokkanConstants.LinkSkills[Random.Next(0, DokkanConstants.LinkSkills.Count)];
        }

        public LinkSkill GetRandomLinkSkill(Tier tier)
        {
            var links = CreateSeededCollection(DokkanConstants.LinkSkills, tier);
            return links[Random.Next(0, links.Count)];
        }

        public Event GetRandomStage()
        {
            return DokkanConstants.Events[Random.Next(0, DokkanConstants.Events.Count)];
        }

        public Category GetRandomCategory()
        {
            return DokkanConstants.Categories[Random.Next(0, DokkanConstants.Categories.Count)];
        }
        public Category GetRandomCategory(Tier tier)
        {
            var cats = CreateSeededCollection(DokkanConstants.Categories, tier);
            return cats[Random.Next(0, cats.Count)];
        }

        public Leader GetRandomLeader()
        {
            return DokkanConstants.Leaders[Random.Next(0, DokkanConstants.Leaders.Count)];
        }

        public Leader GetRandomLeader(Tier tier)
        {
            var leaders = CreateSeededCollection(DokkanConstants.Leaders, tier);
            return leaders[Random.Next(0, leaders.Count)];
        }

        private static List<T> CreateSeededCollection<T>(IEnumerable<T> input, Tier tier) where T : ITieredObject
        {
            List<T> output = [];

            foreach (T item in input)
            {
                int diff = Math.Abs((int)item.Tier - (int)tier);

                if (diff < 2)
                {
                    output.Add(item);

                    if (diff == 0)
                        output.Add(item);
                }
            }

            return output;
        }

        public Challenge GetDailyChallenge()
        {
            if (ChallengeOverride != null) return ChallengeOverride;

            DailyType dailyType = GetRandomDailyType();
            Event todaysEvent = GetRandomStage();
            LinkSkill linkSkill = GetRandomLinkSkill(todaysEvent.Tier);
            Category category = GetRandomCategory(todaysEvent.Tier);
            Leader leader = GetRandomLeader(todaysEvent.Tier);
            Unit todaysUnit = DokkanDailyHelper.GetUnit(leader);

            return new(dailyType, todaysEvent, linkSkill, category, leader, todaysUnit);
        }
    }
}