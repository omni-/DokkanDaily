using DokkanDaily.Models;

namespace DokkanDaily.Constants
{
    public static class DDConstants
    {
        private static List<LinkSkill> linkSkills = 
        [
            new("All in the Family", Difficulty.PissEasy),
            new("Android Assault", Difficulty.Medium),
            new("Battlefield Diva", Difficulty.Extreme),
            new("Big Bad Bosses", Difficulty.Low),
            new("Braniacs", Difficulty.High),
            new("Brutal Beatdown", Difficulty.Medium)
        ];

        private static List<Unit> units = new List<Unit>();

        private static List<Stage> stages = 
        [
            new("Omega Shenron", Difficulty.Medium)
        ];

        public static IReadOnlyList<LinkSkill> LinkSkills { get => linkSkills; }
        public static IReadOnlyDictionary<string, LinkSkill> LinkSkillMap { get; }
        public static IReadOnlyList<DailyType> DailyTypes { get; } 
        public static IReadOnlyList<Stage> Stages { get => stages; }

        static DDConstants()
        {
            LinkSkillMap = new Dictionary<string, LinkSkill>(linkSkills.Select(x => new KeyValuePair<string, LinkSkill>(x.Name, x)));
            DailyTypes = [DailyType.Category, DailyType.LinkSkill, DailyType.Character];
    }
    }
}
