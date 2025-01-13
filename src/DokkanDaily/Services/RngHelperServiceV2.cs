using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class RngHelperServiceV2 : IRngHelperService
    {
        private static DateTime Now => DateTime.UtcNow;
        private static TempCache<Challenge> Challenge = null;
        private static int Seed;
        private readonly IDokkanDailyRepository dokkanDailyRepository;
        private readonly ILogger<RngHelperServiceV2> _logger;
        private readonly DokkanDailySettings _settings;

        public RngHelperServiceV2(IDokkanDailyRepository repository, IOptions<DokkanDailySettings> settings, ILogger<RngHelperServiceV2> logger)
        {
            _logger = logger;
            dokkanDailyRepository = repository;
            Seed = CalcSeed(Now);
            _settings = settings.Value;
        }

        public async Task<Challenge> GetDailyChallenge()
        {
            if (Challenge != null && !Challenge.CacheExpired)
                return Challenge.Value;

            Challenge = await CalcChallenge();

            return Challenge.Value;
        }

        private async Task<TempCache<Challenge>> CalcChallenge()
        {
            Random r = new(Seed);

            // first, get recent challenge history
            IEnumerable<Challenge> recentChallenges;

            try
            {
                // todo: experiment
                // DateTime cutoffDate = DateTime.UtcNow - TimeSpan.FromDays(InternalConstants.ChallengeRepeatLimitDays);

                var dbChallenges = await dokkanDailyRepository.GetChallengeList(null);

                recentChallenges = dbChallenges
                    .Select(x =>
                    {
                        DailyType type = Enum.Parse<DailyType>(x.DailyTypeName);
                        // should be == instead of StartsWith here, but i messed up and made the varchar column too small
                        Stage stage = DokkanConstants.Stages.First(y => y.Name.StartsWith(x.Event) && y.StageNumber == x.Stage);
                        // same here 
                        Leader leader = x.LeaderFullName == null ? null : DokkanConstants.Leaders.First(y => y.FullName.StartsWith(x.LeaderFullName));
                        LinkSkill skill = x.LinkSkill == null ? null : DokkanConstants.LinkSkillMap[x.LinkSkill];
                        Category category = x.Category == null ? null : DokkanConstants.CategoryMap[x.Category];
                        Unit unit = x.LeaderFullName == null ? null : DokkanDailyHelper.GetUnit(leader);

                        return new Challenge(type, stage, skill, category, leader, unit);
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered exception while trying to get recent challenges");
                recentChallenges = [];
            }

            // create comparers
            var stageComparer = EqualityComparer<Stage>.Create((x, y) => x.FullName == y.FullName, x => x.FullName.GetHashCode());
            var leaderComparer = EqualityComparer<Leader>.Create((x, y) => x.FullName == y.FullName, x => x.FullName.GetHashCode());
            var linkSkillComparer = EqualityComparer<LinkSkill>.Create((x, y) => x.Name == y.Name, x => x.Name.GetHashCode());
            var categoryComparer = EqualityComparer<Category>.Create((x, y) => x.Name == y.Name, x => x.Name.GetHashCode());

            // filter out things we've done recently
            var stages = DokkanConstants.Stages
                .Except(recentChallenges
                    .Take(_settings.StageRepeatLimitDays)
                    .Select(x => x.TodaysEvent), stageComparer);
            var leaders = DokkanConstants.Leaders
                .Except(recentChallenges
                    .Where(x => x.DailyType == DailyType.Character)
                    .Take(50)
                    .Select(x => x.Leader), leaderComparer);
            var linkSkills = DokkanConstants.LinkSkills
                .Except(recentChallenges
                    .Where(x => x.DailyType == DailyType.LinkSkill)
                    .Take(30)
                    .Select(x => x.LinkSkill), linkSkillComparer);
            var categories = DokkanConstants.Categories
                .Except(recentChallenges
                    .Where(x => x.DailyType == DailyType.Category)
                    .Take(40)
                    .Select(x => x.Category), categoryComparer);
            var events = stages
                .Select(x => x.Name)
                .Except(recentChallenges
                    .Take(_settings.EventRepeatLimitDays)
                    .Select(x => x.TodaysEvent.Name))
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

            return new(new(dailyType, todaysStage, linkSkill, category, leader, unit));
        }

        private static int CalcSeed(DateTime date)
        {
            var seed = (date.Year * 100000) + (date.DayOfYear * 100);
            return seed;
        }

        public DailyType? GetTodaysDailyType()
        {
            return Challenge?.Value?.DailyType;
        }

        public int GetRawSeed()
        {
            return Seed;
        }

        public void OverrideChallenge(DailyType type, Stage e, LinkSkill link, Category cat, Leader l)
        {
            Challenge = new(new(type, e, link, cat, l, DokkanDailyHelper.GetUnit(l)));
        }

        public void OverrideChallengeType(DailyType type)
        {
            if (Challenge != null)
                Challenge.Value.DailyType = type;
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
            return Challenge.Value;
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
