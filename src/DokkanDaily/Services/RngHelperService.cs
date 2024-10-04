using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class RngHelperService : IRngHelperService
    {
        private static readonly int MaxCopies = 3;

        private Random Random => GetDailySeed();
        private static int SeedOffset { get; set; }
        private static int? SeedOverride { get; set; }
        public static Challenge ChallengeOverride { get; private set; }
        public static DailyType? DailyTypeOverride {  get; private set; }

        public Random GetDailySeed()
        {
            return new Random(GetRawSeed());
        }

        public void OverrideChallenge(DailyType type, Event e, LinkSkill link, Category cat, Leader l)
        {
            ChallengeOverride = new(type, e, link, cat, l, DDConstants.GetUnit(l));
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
			var seed = (date.Year * 1000 + date.DayOfYear) - (SeedOffset * 50);

            return seed;
		}

        public DailyType GetRandomDailyType()
        {
            if (DailyTypeOverride != null) return DailyTypeOverride.Value;

            // link skill challenges are harder and less varied, so they should appear slightly less often
            var types = new List<DailyType>(DDConstants.DailyTypes);
            types.Remove(DailyType.LinkSkill);
            types = [..types.Concat(new List<DailyType>(DDConstants.DailyTypes))];
            return types[Random.Next(0, types.Count)];
        }

        public LinkSkill GetRandomLinkSkill()
        {
            return DDConstants.LinkSkills[Random.Next(0, DDConstants.LinkSkills.Count)];
        }

        public LinkSkill GetRandomLinkSkill(Tier tier)
        {
            var links = CreateSeededCollection(DDConstants.LinkSkills, tier);
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
        public Category GetRandomCategory(Tier tier)
        {
            var cats = CreateSeededCollection(DDConstants.Categories, tier);
            return cats[Random.Next(0, cats.Count)];
        }

        public Leader GetRandomLeader()
        {
            return DDConstants.Leaders[Random.Next(0, DDConstants.Leaders.Count)];
        }

        public Leader GetRandomLeader(Tier tier)
        {
            var leaders = CreateSeededCollection(DDConstants.Leaders, tier);
            return leaders[Random.Next(0, leaders.Count)];
        }

        private static List<T> CreateSeededCollection<T>(IEnumerable<T> input, Tier tier) where T : ITieredObject
        {
            List<T> output = [];

            var tmp = input.Where(x => x.Tier >= tier || (int)x.Tier == (int)tier - 1);

            foreach (T item in tmp)
            {
                if (item.Tier >= tier)
                {
                    int diff = (int)item.Tier - (int)tier;
                    for (int i = 0; i < MaxCopies - diff; i++) 
                    {
                        output.Add(item);
                    }
                } 
                else 
                { 
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
            Unit todaysUnit = DDConstants.GetUnit(leader);

            return new (dailyType, todaysEvent, linkSkill, category, leader, todaysUnit);
        }
    }
}