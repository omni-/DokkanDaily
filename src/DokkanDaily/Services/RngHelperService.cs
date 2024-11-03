using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services
{
    public class RngHelperService : IRngHelperService
    {
        private static int SeedOffset { get; set; }
        private static int? SeedOverride { get; set; }
        public static Challenge ChallengeOverride { get; private set; }
        public static DailyType? DailyTypeOverride { get; private set; }

        // new up random before each use so as not to get weird/varying results
        private static Random GetDailySeededRandom(DateTime? dateOverride = null)
        {
            return new Random(GetRawSeed(dateOverride));
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

        public int GetRawSeed() => GetRawSeed(null);
        private static int GetRawSeed(DateTime? dateOverride)
        {
            if (SeedOverride != null) return SeedOverride.Value;

            var date = dateOverride ?? DateTime.UtcNow.Date;
            var seed = (date.Year * 1000 + date.DayOfYear) / (SeedOffset + 1);
            return seed;
        }

        public DailyType GetRandomDailyType() => GetRandomDailyType(null);
        private static DailyType GetRandomDailyType(DateTime? dateOverride)
        {
            Random random = GetDailySeededRandom(dateOverride);
            if (DailyTypeOverride != null) return DailyTypeOverride.Value;

            // link skill challenges are harder and less varied, so they should appear slightly less often
            var types = new List<DailyType>(DokkanConstants.DailyTypes);
            types.Remove(DailyType.LinkSkill);
            types = [.. types.Concat(new List<DailyType>(DokkanConstants.DailyTypes))];
            return types[random.Next(0, types.Count)];
        }

        public LinkSkill GetRandomLinkSkill() => GetRandomLinkSkill(null);
        private static LinkSkill GetRandomLinkSkill(DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            return DokkanConstants.LinkSkills[random.Next(0, DokkanConstants.LinkSkills.Count)];
        }

        public LinkSkill GetRandomLinkSkill(Tier tier) => GetRandomLinkSkill(tier, null);
        private static LinkSkill GetRandomLinkSkill(Tier tier, DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            var links = CreateSeededCollection(DokkanConstants.LinkSkills, tier);
            return links[random.Next(0, links.Count)];
        }

        public Event GetRandomStage() => GetRandomStage(null);
        private static Event GetRandomStage(DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            return DokkanConstants.Events[random.Next(0, DokkanConstants.Events.Count)];
        }

        public Category GetRandomCategory() => GetRandomCategory(null);
        private static Category GetRandomCategory(DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            return DokkanConstants.Categories[random.Next(0, DokkanConstants.Categories.Count)];
        }

        public Category GetRandomCategory(Tier tier) => GetRandomCategory(tier, null);
        private static Category GetRandomCategory(Tier tier, DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            var cats = CreateSeededCollection(DokkanConstants.Categories, tier);
            return cats[random.Next(0, cats.Count)];
        }

        public Leader GetRandomLeader() => GetRandomLeader(null);
        private static Leader GetRandomLeader(DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            return DokkanConstants.Leaders[random.Next(0, DokkanConstants.Leaders.Count)];
        }

        public Leader GetRandomLeader(Tier tier) => GetRandomLeader(tier, null);

        public Challenge GetTomorrowsChallenge() => GetChallenge((DateTime.UtcNow + TimeSpan.FromDays(1)).Date);

        public Challenge GetDailyChallenge() => GetChallenge(null);

        public Challenge GetDailyChallenge(DateTime dateOverride) => GetChallenge(dateOverride);

        private static Challenge GetChallenge(DateTime? dateOverride = null)
        {
            if (ChallengeOverride != null) return ChallengeOverride;

            DailyType dailyType = GetRandomDailyType(dateOverride);
            Event todaysEvent = GetRandomStage(dateOverride);
            LinkSkill linkSkill = GetRandomLinkSkill(todaysEvent.Tier, dateOverride);
            Category category = GetRandomCategory(todaysEvent.Tier, dateOverride);
            Leader leader = GetRandomLeader(todaysEvent.Tier, dateOverride);

            Unit todaysUnit = DokkanDailyHelper.GetUnit(leader);

            return new(dailyType, todaysEvent, linkSkill, category, leader, todaysUnit);
        }

        private static Leader GetRandomLeader(Tier tier, DateTime? dateOverride = null)
        {
            Random random = GetDailySeededRandom(dateOverride);
            var leaders = CreateSeededCollection(DokkanConstants.Leaders, tier);
            return leaders[random.Next(0, leaders.Count)];
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
    }
}