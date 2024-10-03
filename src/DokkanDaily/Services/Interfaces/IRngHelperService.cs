using DokkanDaily.Models;
using DokkanDaily.Models.Enums;

namespace DokkanDaily.Services.Interfaces
{
    public interface IRngHelperService
    {
        Random GetDailySeed();

        Event GetRandomStage();

        DailyType GetRandomDailyType();

        LinkSkill GetRandomLinkSkill();

        LinkSkill GetRandomLinkSkill(Tier minTier);

        Category GetRandomCategory();

        Category GetRandomCategory(Tier minTier);

        Leader GetRandomLeader();

        Leader GetRandomLeader(Tier minTier);

        Challenge GetDailyChallenge();

        void Reset();

        void SetDailySeed(int seed);

        void RollDailySeed();

        void OverrideChallenge(DailyType type, Event e, LinkSkill link, Category cat, Leader l);

        void OverrideChallengeType(DailyType type);

		int GetRawSeed();
	}
}
