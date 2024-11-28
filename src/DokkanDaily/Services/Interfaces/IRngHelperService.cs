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

        void OverrideChallenge(DailyType type, Stage e, LinkSkill link, Category cat, Leader l);

        void OverrideChallengeType(DailyType type);

        int GetRawSeed();
    }
}
