using DokkanDaily.Models;
using DokkanDaily.Models.Enums;

namespace DokkanDaily.Services.Interfaces
{
    public interface IRngHelperService
    {
        DailyType? GetTodaysDailyType();

        Task<Challenge> GetDailyChallenge();

        Task<Challenge> UpdateDailyChallenge();

        Task SetDailySeed(int seed);

        Task RollDailySeed();

        Task Reset();

        Task OverrideChallenge(DailyType type, Stage e, LinkSkill link, Category cat, Leader l, Challenge expected = null);

        Task OverrideChallengeType(DailyType type, Challenge expected);

        int GetRawSeed();
    }
}
