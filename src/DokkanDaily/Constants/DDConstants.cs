using DokkanDaily.Models;

namespace DokkanDaily.Constants
{
    public static class DDConstants
    {
        private static List<LinkSkill> linkSkills = 
        [
            new("All in the Family", Tier.S),
            new("Android Assault", Tier.C),
            new("Battlefield Diva", Tier.F),
            new("Big Bad Bosses", Tier.A),
            new("Braniacs", Tier.E),
            new("Brutal Beatdown", Tier.C)
        ];

        private static List<Unit> leaders =
        [
            new("Newfound Power Beyond the Ultimate", "Gohan (Beast)", Tier.S),
            new("Transcendent Divine Power", "Goku (Ultra Instinct -Sign-)", Tier.S),
            new("Fierce Clash of Burning Power", "Super Saiyan Goku & Super Saiyan Gohan (Youth) & Super Saiyan Trunks (Teen)", Tier.S),
            new("Power of an Isolated Iron Wall", "Jiren", Tier.S),
            new("True Warrior Race", "Saiyan God SS Evolved Vegeta", Tier.S),
            new("Radiantly Shining Heroes", "Gamma 1 & Gamma 2/Gamma 1", Tier.S)
        ];

        private static List<Event> events = 
        [
            new("Omega Shenron", Tier.C, "ShadowDragons", 8),
            new("Fighting Legend: Goku", Tier.F, "LGE"),
            new("Fighting Legend: Goku [GT Edition]", Tier.F, "LGT"),
            new("Fighting Legend: Vegeta", Tier.E, "LVE"),
            new("Fighting Legend: Frieza", Tier.E, "LFE"),
            new("The Devil Awakens", Tier.A, "DevilAwakens"),
            new("Supreme Magnificent Battle [Movie Edition]", Tier.C, "SMB_MOVIE", 3),
            new("Supreme Magnificent Battle [Universe Survival Saga]", Tier.S, "SMB_USS", 4)

        ];

        private static List<Category> categories =
        [
            new("Accelerated Battle", Tier.A),
            new("All-Out Struggle", Tier.A),
            new("Androids", Tier.C),
            new("Androids/Cell Saga", Tier.C),
            new("Artificial Life Forms", Tier.B),
            new("Battle of Fate", Tier.S)
        ];

        public static IReadOnlyList<LinkSkill> LinkSkills { get => linkSkills; }
        public static IReadOnlyDictionary<string, LinkSkill> LinkSkillMap { get; }
        public static IReadOnlyList<DailyType> DailyTypes { get; } 
        public static IReadOnlyList<Event> Events { get => events; }
        public static IReadOnlyList<Category> Categories { get => categories; }
        public static IReadOnlyList<Unit> Leaders { get => leaders; }

        static DDConstants()
        {
            LinkSkillMap = new Dictionary<string, LinkSkill>(linkSkills.Select(x => new KeyValuePair<string, LinkSkill>(x.Name, x)));
            DailyTypes = [DailyType.Category, DailyType.LinkSkill, DailyType.Character];
        }
    }
}
