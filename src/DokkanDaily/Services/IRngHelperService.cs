using DokkanDaily.Models;
using DokkanDaily.Models.Enums;

namespace DokkanDaily.Services
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
    }
}
