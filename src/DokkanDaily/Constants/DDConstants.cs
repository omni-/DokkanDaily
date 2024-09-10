using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;

namespace DokkanDaily.Constants
{
    public static class DDConstants
    {
        private static List<LinkSkill> linkSkills =
        [
            new("All in the Family", Tier.S),
            new("Android Assault", Tier.C),
            new("Battlefield Diva", Tier.E),
            new("Big Bad Bosses", Tier.A),
            new("Braniacs", Tier.D),
            new("Brutal Beatdown", Tier.C),
            new("Cold Judgement", Tier.B),
            new("Dismal Future", Tier.A),
            new("Experienced Fighters", Tier.S),
            new("Fear and Faith", Tier.D),
            new("Fused Fighter", Tier.A),
            new("Gaze of Respect", Tier.F),
            new("Godly Power", Tier.S),
            new("Golden Warrior", Tier.S),
            new("GT", Tier.D),
            new("Guidance of the Dragon Balls", Tier.B),
            new("Hero of Justice", Tier.C),
            new("Infighter", Tier.A),
            new("Infinite Regeneration", Tier.B),
            new("Kamehameha", Tier.S),
            new("Limit-Breaking Form", Tier.E),
            new("Majin", Tier.S),
            new("Messenger from the Future", Tier.F),
            new("Metamorphosis", Tier.C),
            new("Namekians", Tier.F),
            new("Nightmare", Tier.C),
            new("Over in a Flash", Tier.A),
            new("Power Bestowed by God", Tier.B),
            new("Prodigies", Tier.D),
            new("Revival", Tier.F),
            new("Royal Lineage", Tier.A),
            new("Saiyan Pride", Tier.D),
            new("Saiyan Roar", Tier.D),
            new("Saiyan Warrior Race", Tier.S),
            new("Shocking Speed", Tier.S),
            new("Signature Pose", Tier.C),
            new("Solid Support", Tier.C),
            new("Strongest Clan in Space", Tier.D),
            new("Super Saiyan", Tier.S),
            new("The Incredible Adventure", Tier.C),
            new("The Innocents", Tier.E),
            new("The Saiyan Lineage", Tier.S),
            new("The Wall Standing Tall", Tier.B),
            new("Thirst for Conquest", Tier.D),
            new("Tournament of Power", Tier.S),
            new("Universe's Most Malevolent", Tier.E),
            new("Warrior Gods", Tier.B),
            new("Warriors of Universe 6", Tier.C),
            new("Z Fighters", Tier.E)
        ];

        private static List<Leader> leaders =
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
            new("Fighting Legend: Goku [GT Edition]", Tier.E, "LGT"),
            new("Fighting Legend: Vegeta", Tier.E, "LVE"),
            new("Fighting Legend: Frieza", Tier.D, "LFE"),
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
            new("Battle of Fate", Tier.S),
            new("Battle of Wits", Tier.E),
            new("Bond of Friendship", Tier.F),
            new("Bond of Master and Disciple", Tier.S),
            new("Bond of Parent and Child", Tier.S),
            new("Connected Hope", Tier.C),
            new("Corroded Body and Mind", Tier.E),
            new("Crossover", Tier.C),
            new("DB Saga", Tier.C),
            new("Defenders of Justice", Tier.D),
            new("Dragon Ball Heroes", Tier.C),
            new("Earthlings", Tier.F),
            new("Earth-Bred Fighters", Tier.S),
            new("Entrusted Will", Tier.S),
            new("Exploding Rage", Tier.A),
            new("Final Trump Card", Tier.S),
            new("Full Power", Tier.S),
            new("Fused Fighters", Tier.B),
            new("Fusion", Tier.C),
            new("Future Saga", Tier.S),
            new("Giant Ape Power", Tier.E),
            new("Gifted Warriors", Tier.B)
        ];

        private static List<Unit> UnitDB = new List<Unit>();

        public static IReadOnlyDictionary<DokkanType, string> TypeToHexMap = new Dictionary<DokkanType, string>()
        {
            { DokkanType.AGL, "0555D5" },
            { DokkanType.STR, "C4151E" },
            { DokkanType.PHY, "B46D00" },
            { DokkanType.INT, "883BA9" },
            { DokkanType.TEQ, "008806" }
        }.AsReadOnly();

        public static IReadOnlyList<LinkSkill> LinkSkills { get => linkSkills; }
        public static IReadOnlyDictionary<string, LinkSkill> LinkSkillMap { get; }
        public static IReadOnlyList<DailyType> DailyTypes { get; } 
        public static IReadOnlyList<Event> Events { get => events; }
        public static IReadOnlyList<Category> Categories { get => categories; }
        public static IReadOnlyList<Leader> Leaders { get => leaders; }

        static DDConstants()
        {
            LinkSkillMap = new Dictionary<string, LinkSkill>(linkSkills.Select(x => new KeyValuePair<string, LinkSkill>(x.Name, x)));
            DailyTypes = [DailyType.Category, DailyType.LinkSkill, DailyType.Character];
            UnitDB = DDHelper.BuildCharacterDb().ToList();
        }

        public static Unit GetUnit(string name, string title)
        {
            return UnitDB.FirstOrDefault(x => x.Name == name && x.Title == title);
        }

        public static Unit GetUnit(Leader leader)
        {
            return UnitDB.FirstOrDefault(x => x.Name == leader.Name && x.Title == leader.Title);
        }
    }
}
