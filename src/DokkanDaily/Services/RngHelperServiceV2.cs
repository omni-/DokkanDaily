using DokkanDaily.Configuration;
using DokkanDaily.Constants;
using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using DokkanDaily.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace DokkanDaily.Services
{
    public class RngHelperServiceV2 : IRngHelperService
    {
        private static DateTime Now => DateTime.UtcNow;

        private readonly IDokkanDailyRepository _dokkanDailyRepository;
        private readonly ILogger<RngHelperServiceV2> _logger;
        private readonly DokkanDailySettings _settings;
        private readonly SemaphoreSlim _challengeLock = new(1, 1);

        private Challenge _challenge;
        private int _seed;

        public RngHelperServiceV2(IDokkanDailyRepository repository, IOptions<DokkanDailySettings> settings, ILogger<RngHelperServiceV2> logger)
        {
            _logger = logger;
            _dokkanDailyRepository = repository;
            _settings = settings.Value;
            _seed = CalcSeed(Now);
        }

        public async Task<Challenge> GetDailyChallenge()
        {
            if (TryGetCachedChallenge(out Challenge cached)) return cached;

            await _challengeLock.WaitAsync();
            try
            {
                // another caller may have recalculated while we waited on the lock
                if (TryGetCachedChallenge(out cached)) return cached;

                if (_challenge is not null)
                    _logger.LogInformation("Cached challenge from {@date} expired at {@time}. Re-calculating challenge...", _challenge.Date, _challenge.Date + TimeSpan.FromDays(1));

                _challenge = await CalcChallenge();

                return _challenge;
            }
            finally
            {
                _challengeLock.Release();
            }
        }

        public DailyType? GetTodaysDailyType() => _challenge?.DailyType;

        public int GetRawSeed() => _seed;

        public void OverrideChallenge(DailyType type, Stage e, LinkSkill link, Category cat, Leader l)
            => _challenge = new(type, e, link, cat, l, DokkanDailyHelper.GetUnitOrDefault(l), DateTime.UtcNow);

        public void OverrideChallengeType(DailyType type)
        {
            Challenge current = _challenge;

            if (current is null)
            {
                _logger.LogWarning("Cannot override the daily type: no challenge has been calculated yet.");
                return;
            }

            if (!HasTargetFor(current, type))
            {
                _logger.LogWarning("Cannot override the daily type to {Type}: today's challenge has no {Type} target to show.", type, type);
                return;
            }

            _challenge = new(type, current.TodaysEvent, current.LinkSkill, current.Category, current.Leader, current.TodaysUnit, current.Date);
        }

        public async Task Reset()
        {
            await _challengeLock.WaitAsync();
            try
            {
                _seed = CalcSeed(Now);
                _challenge = await CalcChallenge();
            }
            finally
            {
                _challengeLock.Release();
            }
        }

        public async Task RollDailySeed()
        {
            await _challengeLock.WaitAsync();
            try
            {
                _seed++;
                _challenge = await CalcChallenge();
            }
            finally
            {
                _challengeLock.Release();
            }
        }

        public async Task SetDailySeed(int seed)
        {
            await _challengeLock.WaitAsync();
            try
            {
                _seed = seed;
                _challenge = await CalcChallenge();
            }
            finally
            {
                _challengeLock.Release();
            }
        }

        public async Task<Challenge> UpdateDailyChallenge()
        {
            await _challengeLock.WaitAsync();
            try
            {
                DateTime tomorrow = Now + TimeSpan.FromDays(1);
                _seed = CalcSeed(tomorrow);
                _challenge = await CalcChallenge(tomorrow);

                return _challenge;
            }
            finally
            {
                _challengeLock.Release();
            }
        }

        private bool TryGetCachedChallenge(out Challenge challenge)
        {
            challenge = _challenge;

            return challenge is not null && DateTime.UtcNow <= challenge.Date + TimeSpan.FromDays(1);
        }

        private async Task<Challenge> CalcChallenge(DateTime? date = null)
        {
            _logger.LogInformation("Calculating challenge using seed {Seed}", _seed);
            Random r = new(_seed);

            // Materialize this once and retain it as the only source for filtering and fallback.
            // A leader with no matching unit entry has no card art to render, so never offer one.
            IReadOnlyList<Leader> baseLeaders = BuildEligibleLeaderBasePool(DokkanConstants.Leaders, DokkanConstants.UnitDB);
            IEnumerable<Leader> leaders = baseLeaders;
            IEnumerable<LinkSkill> linkSkills = DokkanConstants.LinkSkills;
            IEnumerable<Category> categories = DokkanConstants.Categories;
            List<Stage> stages = [.. DokkanConstants.Stages];
            List<string> events = [.. stages
                .Select(x => x.Name)
                .Distinct()];

            try
            {
                // todo: experiment
                // DateTime cutoffDate = DateTime.UtcNow - TimeSpan.FromDays(InternalConstants.ChallengeRepeatLimitDays);

                IEnumerable<DbChallengeProjection> recentChallenges = await GetRecentChallenges(baseLeaders);

                // create comparers
                EqualityComparer<Stage> stageComparer = EqualityComparer<Stage>.Create((x, y) => x.FullName == y.FullName, x => x.FullName.GetHashCode());
                EqualityComparer<Leader> leaderComparer = EqualityComparer<Leader>.Create((x, y) => x.FullName == y.FullName, x => x.FullName.GetHashCode());
                EqualityComparer<LinkSkill> linkSkillComparer = EqualityComparer<LinkSkill>.Create((x, y) => x.Name == y.Name, x => x.Name.GetHashCode());
                EqualityComparer<Category> categoryComparer = EqualityComparer<Category>.Create((x, y) => x.Name == y.Name, x => x.Name.GetHashCode());

                // filter out things we've done recently - only commit the filtered pools once all of them succeed
                List<Stage> filteredStages = [.. stages
                    .Except(recentChallenges
                        .Where(x => x.Stage is not null)
                        .Take(_settings.StageRepeatLimitDays)
                        .Select(x => x.Stage), stageComparer)];
                IEnumerable<Leader> filteredLeaders = leaders
                    .Except(recentChallenges
                        .Where(x => x.DailyType == DailyType.Character && x.Leader is not null)
                        .Take(10)
                        .Select(x => x.Leader), leaderComparer);
                IEnumerable<LinkSkill> filteredLinkSkills = linkSkills
                    .Except(recentChallenges
                        .Where(x => x.DailyType == DailyType.LinkSkill && x.LinkSkill is not null)
                        .Take(10)
                        .Select(x => x.LinkSkill), linkSkillComparer);
                IEnumerable<Category> filteredCategories = categories
                    .Except(recentChallenges
                        .Where(x => x.DailyType == DailyType.Category && x.Category is not null)
                        .Take(10)
                        .Select(x => x.Category), categoryComparer);
                List<string> filteredEvents = [.. filteredStages
                    .Select(x => x.Name)
                    .Except(recentChallenges
                        .Where(x => x.Stage is not null)
                        .Take(_settings.EventRepeatLimitDays)
                        .Select(x => x.Stage.Name))];

                // an over-aggressive repeat limit can filter everything out - keep the unfiltered pool if so
                if (filteredEvents.Count == 0)
                    _logger.LogWarning("Recency filtering removed every event. Falling back to the unfiltered event list.");
                else
                {
                    stages = filteredStages;
                    leaders = filteredLeaders;
                    linkSkills = filteredLinkSkills;
                    categories = filteredCategories;
                    events = filteredEvents;
                }

                _logger.LogInformation("Filtered challenges successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Encountered exception while trying to filter recent challenges. Falling back to the unfiltered pools.");
            }

            // pick an event
            string todaysEvent = events[r.Next(0, events.Count)];
            List<Stage> availableStages = [.. stages.Where(x => x.Name == todaysEvent)];
            Stage todaysStage = availableStages[r.Next(0, availableStages.Count)];
            Tier t = todaysStage.Tier;

            // only offer a daily type we still have a tier-appropriate target for
            List<DailyType> availableTypes = [];
            if (HasTierAppropriateEntry(categories, t)) availableTypes.Add(DailyType.Category);
            if (HasTierAppropriateEntry(leaders, t)) availableTypes.Add(DailyType.Character);
            if (HasTierAppropriateEntry(linkSkills, t)) availableTypes.Add(DailyType.LinkSkill);

            if (availableTypes.Count == 0)
            {
                _logger.LogWarning("No daily type has a target within one tier of {Tier}. Falling back to the full type list.", t);
                availableTypes = [.. DokkanConstants.DailyTypes];
            }

            DailyType dailyType = availableTypes[r.Next(0, availableTypes.Count)];

            // fill out the challenge details
            Leader leader = PickWithFallback(leaders, baseLeaders, r, t, "leaders");
            LinkSkill linkSkill = PickWithFallback(linkSkills, DokkanConstants.LinkSkills, r, t, "link skills");
            Category category = PickWithFallback(categories, DokkanConstants.Categories, r, t, "categories");
            Unit unit = DokkanDailyHelper.GetUnitOrDefault(leader);

            DateTime cacheDate = date ?? DateTime.UtcNow;

            Challenge challenge = new(dailyType, todaysStage, linkSkill, category, leader, unit, cacheDate);

            string logMessage = $"{{ DailyType: {dailyType}, Stage: {todaysStage.FullName}, Target: {dailyType switch { DailyType.LinkSkill => linkSkill?.Name, DailyType.Category => category?.Name, DailyType.Character => leader?.FullName, _ => null }} }}";

            _logger.LogInformation("Calculated new challenge: {msg} Expires at {@dt}UTC", logMessage, cacheDate.Date + TimeSpan.FromDays(1));

            return challenge;
        }

        /// <summary>
        /// Materialises the persisted challenge history into the in-memory model, most recent first.
        /// </summary>
        private async Task<IEnumerable<DbChallengeProjection>> GetRecentChallenges(IReadOnlyList<Leader> baseLeaders)
        {
            IEnumerable<DbChallenge> dbChallenges = await _dokkanDailyRepository.GetChallengeList(null);

            _logger.LogInformation("Retrieved {count} challenges.", dbChallenges.Count());

            return [.. dbChallenges.Select(x =>
            {
                DailyType type = Enum.Parse<DailyType>(x.DailyTypeName);
                // should be == instead of StartsWith here, but i messed up and made the varchar column too small
                Stage stage = DokkanConstants.Stages.FirstOrDefault(y => y.Name.StartsWith(x.Event) && y.StageNumber == x.Stage);
                // same here
                Leader leader = x.LeaderFullName is null ? null : baseLeaders.FirstOrDefault(y => y.FullName.StartsWith(x.LeaderFullName));
                LinkSkill skill = x.LinkSkill is null ? null : DokkanConstants.LinkSkillMap.GetValueOrDefault(x.LinkSkill);
                Category category = x.Category is null ? null : DokkanConstants.CategoryMap.GetValueOrDefault(x.Category);

                return new DbChallengeProjection(type, stage, skill, category, leader);
            })];
        }

        private static int CalcSeed(DateTime date) => (date.Year * 100000) + (date.DayOfYear * 100);

        private static bool HasTargetFor(Challenge challenge, DailyType type) => type switch
        {
            DailyType.Character => challenge.Leader is not null,
            DailyType.Category => challenge.Category is not null,
            DailyType.LinkSkill => challenge.LinkSkill is not null,
            _ => false
        };

        private static bool HasTierAppropriateEntry<T>(IEnumerable<T> pool, Tier t) where T : ITieredObject
            => pool.Any(x => Math.Abs((int)x.Tier - (int)t) < 2);

        internal IReadOnlyList<Leader> BuildEligibleLeaderBasePool(IEnumerable<Leader> leaders, IEnumerable<Unit> units)
        {
            HashSet<(string Name, string Title)> unitKeys = units
                .Select(x => (x.Name, x.Title))
                .ToHashSet();
            IReadOnlyList<Leader> eligible = [.. leaders.Where(x => unitKeys.Contains((x.Name, x.Title)))];

            if (eligible.Count > 0) return eligible;

            const string message = "Cannot calculate a character challenge because no configured leader has a matching unit.";
            _logger.LogCritical(message);
            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Picks a tier-appropriate entry, widening the search when the recency-filtered pool is
        /// exhausted so that challenge generation can never yield a null target.
        /// </summary>
        internal T PickWithFallback<T>(IEnumerable<T> filtered, IReadOnlyList<T> unfiltered, Random r, Tier t, string poolName) where T : class, ITieredObject
        {
            T pick = Pick(filtered, r, t);
            if (pick is not null) return pick;

            _logger.LogWarning("No tier-appropriate entry left in the filtered {Pool} pool for tier {Tier}. Falling back to the full pool.", poolName, t);

            pick = Pick(unfiltered, r, t);
            if (pick is not null) return pick;

            _logger.LogWarning("No entry in the {Pool} pool is within one tier of {Tier}. Falling back to the closest tier available.", poolName, t);

            return unfiltered.MinBy(x => Math.Abs((int)x.Tier - (int)t));
        }

        private static T Pick<T>(IEnumerable<T> input, Random r, Tier t) where T : class, ITieredObject
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

            return output.Count == 0 ? null : output[r.Next(0, output.Count)];
        }

        /// <summary>
        /// A persisted challenge resolved back to the in-memory reference data it was generated from.
        /// </summary>
        private sealed record DbChallengeProjection(DailyType DailyType, Stage Stage, LinkSkill LinkSkill, Category Category, Leader Leader);
    }
}
