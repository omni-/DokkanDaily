using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;

namespace DokkanDaily.Services
{
    public class RngHelperServiceV2 : IRngHelperService
    {
        private static DateTime Now => DateTime.UtcNow;
        private static Challenge Challenge = null;
        private static int Seed;
        private readonly IDokkanDailyRepository dokkanDailyRepository;
        private readonly ILogger<RngHelperServiceV2> _logger;

        public RngHelperServiceV2(IDokkanDailyRepository repository, ILogger<RngHelperServiceV2> logger)
        {
            _logger = logger;
            dokkanDailyRepository = repository;
            Seed = CalcSeed(Now);
        }

        public async Task<Challenge> GetDailyChallenge()
        {
            if (Challenge != null)
                return Challenge;

            Challenge = await CalcChallenge();

            return Challenge;
        }

        private async Task<Challenge> CalcChallenge()
        {
            Random r = new(Seed);

            // first, get recent challenge history
            List<Challenge> recentChallenges;

            try
            {
                DateTime cutoffDate = DateTime.UtcNow - TimeSpan.FromDays(InternalConstants.ChallengeRepeatLimitDays);
                var dbChallenges = await dokkanDailyRepository.GetChallengeList(cutoffDate);

                recentChallenges = dbChallenges.Select(x =>
                {
                    DailyType type = Enum.Parse<DailyType>(x.DailyTypeName);
                    Stage stage = DokkanConstants.Stages.First(y => y.Name == x.Event && y.StageNumber == x.Stage);
                    Leader leader = DokkanConstants.Leaders.First(y => y.FullName == x.LeaderFullName);
                    LinkSkill skill = DokkanConstants.LinkSkillMap[x.LinkSkill];
                    Category category = DokkanConstants.Categories.First(y => y.Name == x.Category);
                    Unit unit = DokkanDailyHelper.GetUnit(leader);

                    return new Challenge(type, stage, skill, category, leader, unit);
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered exception while trying to get recent challenges");
                recentChallenges = [];
            }

            // filter out things we've done recently
            var stages = DokkanConstants.Stages
                .Except(recentChallenges
                    .Select(x => x.TodaysEvent));
            var leaders = DokkanConstants.Leaders
                .Except(recentChallenges
                    .Select(x => x.Leader));
            var linkSkills = DokkanConstants.LinkSkills
                .Except(recentChallenges
                    .Select(x => x.LinkSkill));
            var categories = DokkanConstants.Categories
                .Except(recentChallenges
                    .Select(x => x.Category));
            var events = stages
                .Select(x => x.Name)
                .Distinct()
                .ToList();

            // pick an event
            string todaysEvent = events[r.Next(0, events.Count)];
            var availableStages = stages
                .Where(x => x.Name == todaysEvent)
                .ToList();
            Stage todaysStage = availableStages[r.Next(0, availableStages.Count)];
            Tier t = todaysStage.Tier;

            // pick a daily type
            DailyType dailyType = DokkanConstants.DailyTypes[r.Next(0, DokkanConstants.DailyTypes.Count)];

            // fill out the challenge details
            Leader leader = Pick(leaders, r, t);
            LinkSkill linkSkill = Pick(linkSkills, r, t);
            Category category = Pick(categories, r, t);

            Unit unit = DokkanDailyHelper.GetUnit(leader);

            return new Challenge(dailyType, todaysStage, linkSkill, category, leader, unit);
        }

        private static int CalcSeed(DateTime date)
        {
            var seed = (date.Year * 100000) + (date.DayOfYear * 100);
            return seed;
        }

        public DailyType? GetTodaysDailyType()
        {
            return Challenge?.DailyType;
        }


        public int GetRawSeed()
        {
            return Seed;
        }

        public void OverrideChallenge(DailyType type, Stage e, LinkSkill link, Category cat, Leader l)
        {
            Challenge = new(type, e, link, cat, l, DokkanDailyHelper.GetUnit(l));
        }

        public void OverrideChallengeType(DailyType type)
        {
            Challenge.DailyType = type;
        }

        public async Task Reset()
        {
            Seed = CalcSeed(Now);
            Challenge = await CalcChallenge();
        }

        public async Task RollDailySeed()
        {
            Seed++;
            Challenge = await CalcChallenge();
        }

        public async Task SetDailySeed(int seed)
        {
            Seed = seed;
            Challenge = await CalcChallenge();
        }

        public async Task<Challenge> UpdateDailyChallenge()
        {
            Seed = CalcSeed(Now + TimeSpan.FromDays(1));
            Challenge = await CalcChallenge();
            return Challenge;
        }

        private static T Pick<T>(IEnumerable<T> input, Random r, Tier t) where T : ITieredObject
        {
            List<T> output = [];

            foreach (T item in input)
            {
                int diff = Math.Abs((int)item.Tier - (int)t);

                if (diff < 2)
                {
                    output.Add(item);

                    if (diff == 0)
                        output.Add(item);
                }
            }
            return output[r.Next(0, output.Count)];
        }
    }
}
