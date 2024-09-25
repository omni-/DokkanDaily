using DokkanDaily.Helpers;
using DokkanDaily.Models;
using DokkanDaily.Models.Enums;
using System.Text.RegularExpressions;

namespace DokkanDaily.Constants
{
    public static partial class DDConstants
    {
        #region Link Skills
        public static IReadOnlyList<LinkSkill> LinkSkills { get => linkSkills; }
        public static IReadOnlyDictionary<string, LinkSkill> LinkSkillMap { get; }

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
            new("Metamorphosis", Tier.D),
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
        #endregion

        #region Leaders
        public static IReadOnlyList<Leader> Leaders { get => leaders; }

        private static List<Leader> leaders =
        [
            //super
            new("Newfound Power Beyond the Ultimate", "Gohan (Beast)", Tier.S),
            new("Transcendent Divine Power", "Goku (Ultra Instinct -Sign-)", Tier.S),
            new("Fierce Clash of Burning Power", "Super Saiyan Goku & Super Saiyan Gohan (Youth) & Super Saiyan Trunks (Teen)", Tier.S),
            new("True Warrior Race", "Super Saiyan God SS Evolved Vegeta", Tier.S),
            new("Radiantly Shining Heroes", "Gamma 1 & Gamma 2/Gamma 1", Tier.S),
            new("True Ultra Instinct", "Goku (Ultra Instinct)", Tier.S),
            new("Power Beyond the Extremes", "Gohan (Teen)", Tier.S),
            new("Scattered Reflection of Overflowing Fighting Spirit", "Super Saiyan Gogeta", Tier.S),
            new("Pressed-for-Time Showdown", "Super Saiyan 3 Goku (Angel)", Tier.S),
            new("Beyond Boundless Power", "Super Saiyan God SS Goku (Kaioken) & Super Saiyan God SS Evolved Vegeta", Tier.B),
            new("Fused Hope", "Goku & Vegeta (Angel)", Tier.S),
            new("Transcendent Height", "Kefla", Tier.A),
            new("Determination to Protect Universe 11", "Toppo", Tier.S),
            new("Astounding Fusion Power", "Gotenks", Tier.C),
            new("Indomitable Fighter", "Super Saiyan Gohan (Future)", Tier.S),
            new("Greatest Evolution of Power", "Super Saiyan 4 Goku", Tier.A),
            new("All-Out Ultimate Battle", "Super Saiyan Goku & Super Saiyan Vegeta & Super Saiyan Trunks (Teen)", Tier.S),
            new("Exploding Fist", "Super Saiyan 3 Goku", Tier.S),
            new("Fighter Entrusted with Allies' Wishes", "Super Saiyan God Goku", Tier.S),
            new("Exploding Latent Power", "Piccolo (Power Awakening)", Tier.S),
            new("All-Out Final Battle", "Super Saiyan God SS Goku & Super Saiyan God SS Vegeta", Tier.S),
            new("Hope at the End of a Deadly Showdown", "Super Saiyan Trunks (Future)", Tier.A),
            new("Universe's Last Hope", "Super Saiyan 3 Goku & Super Saiyan 2 Vegeta", Tier.A),
            new("Tenacious Secret Plan", "Super Vegeta", Tier.B),
            new("Showdown for the World's Strongest", "Goku", Tier.A),
            new("Ultimate and Invincible Fusion", "Vegito", Tier.B),
            new("Ultimate and Supreme Fusion", "Gogeta", Tier.B),
            new("Awakened Super Hero", "Ultimate Gohan", Tier.A),
            new("Heroic Warrior", "Gohan (Future)", Tier.B),
            new("The Power to Protect", "Gohan (Kid)", Tier.E),
            new("Genius Scientist's Courage and Resolve", "Android 21 (Normal)", Tier.A),
            new("Epic Defensive Battle", "Piccolo & Gohan (Kid)/Gohan (Kid)", Tier.B),
            new("Extraordinary Super Warrior", "Super Saiyan Goku", Tier.B),
            new("Intense Struggle for Survival", "Androids 17 & 18", Tier.S),
            new("Awakened True Power", "Super Saiyan Gohan (Youth)", Tier.C),
            new("Miracle-Calling Clash", "Super Saiyan Gohan (Teen) & Super Saiyan Goten (Kid)", Tier.C),
            new("Raging Counterattack", "Bardock", Tier.E),
            new("A Soul Pumped Up in Battle", "Goku", Tier.B),
            new("Quest for the Dragon Balls", "Goku (Youth)", Tier.C),
            new("Super Power Counterattack", "Super Saiyan Trunks (Teen)", Tier.E),
            new("Fusion Reborn", "Goku (Angel) & Vegeta (Angel)", Tier.F),
            new("Pinnacle of Fury", "Goku", Tier.F),
            new("Righteous Otherworld Defense", "Paikuhan", Tier.F),
            new("Mark of Saiyan Strength", "Super Saiyan 3 Bardock", Tier.E),
            new("Kami and Demon King United", "Piccolo", Tier.D),
            new("Resilient Will to Protect the Future", "Trunks (Teen) (Future)", Tier.C),
            new("Imperturbable Hero", "Gamma 1", Tier.B),
            new("Fearless Hero", "Gamma 2", Tier.C),
            new("Earth-Shaking Immense Power", "Super Saiyan Goku", Tier.D),
            new("Beyond the Shining Clouds", "Gohan (Kid)", Tier.B),
            new("Transcendent Fusion", "Super Saiyan Gogeta", Tier.C),
            new("Divine Warriors with Infinite Power", "Super Saiyan God Goku & Super Saiyan God Vegeta", Tier.C),
            new("Saiyan Warriors with Ultimate Power", "Super Saiyan 4 Goku & Super Saiyan 4 Vegeta", Tier.D),
            new("Earth's Dominant Elite", "Yamcha", Tier.A),
            new("Final Super Power", "Super Saiyan God SS Goku (Kaioken)", Tier.B),
            new("Limitless Combat Power", "Super Saiyan Vegeta", Tier.E),
            new("Hot-Blooded God of Destruction", "Beerus", Tier.C),
            new("Resilient Power of Emotions", "Kale (Berserk)", Tier.C),
            new("Full-Power Final Showdown", "Super Saiyan Goku & Super Saiyan Vegeta", Tier.C),
            new("Warriors Entrusted with Earth's Fate", "Super Saiyan Goku/Super Saiyan Gohan (Youth)", Tier.E),
            new("Absolute Power", "Jiren", Tier.F),
            new("Battle Against the Fate of Annihilation", "Bardock", Tier.E),
            new("Battle to Protect Tomorrow", "Super Saiyan 2 Goku", Tier.E),
            new("Stamina to Attack Evil", "Pan (GT)", Tier.F),
            new("Divine Combat Begins", "Super Saiyan God SS Goku/Super Saiyan God SS Vegeta", Tier.F),
            new("Strike of Full Anger", "Super Saiyan Goku", Tier.F),
            new("Battle to Become the Strongest", "Super Saiyan Goku (GT)", Tier.F),
            new("Battle to Reach the Top", "Super Saiyan Vegeta (GT)", Tier.F),
            new("Boiling Power", "Super Saiyan Goku", Tier.F),
            new("All-Out Super Attack", "Super Saiyan Gohan (Teen)", Tier.S),
            //extreme
            new("Universe-Devastating Combat Power", "Broly", Tier.S),
            new("Surge of Heightened Fighting Spirit", "Super Saiyan Broly", Tier.S),
            new("Awakening of the Prince", "Vegeta", Tier.S),
            new("The Awakened Ego and Joy of Combat", "Majin Buu (Good)", Tier.S),
            new("Infinite Sanctuary", "Fusion Zamasu", Tier.S),
            new("Mastery of the Power of Rage", "Goku Black (Super Saiyan Rosé)", Tier.S),
            new("Beautiful Final View", "Frieza (1st Form)", Tier.A),
            new("Death Match for World Domination", "Piccolo Jr. (Giant Form)", Tier.A),
            new("Invincible Absorption", "Majin Buu (Gotenks)", Tier.S),
            new("Split Into Good and Evil", "Majin Buu (Good)/Majin Buu (Pure Evil)", Tier.A),
            new("Terrifying Phantom Majin", "Hirudegarn", Tier.S),
            new("Instinctive Destruction", "Legendary Super Saiyan Broly", Tier.B),
            new("True Power of a God", "Zamasu", Tier.C),
            new("Planet-Crushing Blow", "Cooler (Final Form)", Tier.D),
            new("The True Value of Perfect Form", "Cell (Perfect Form)", Tier.D),
            new("Demonic Fighter Wielding Forbidden Power", "Turles", Tier.D),
            new("Infinite Terror", "Metal Cooler", Tier.E),
            new("Extreme Ultimate Power", "Cooler", Tier.E),
            new("Foe Elimination Circuit", "Android 13", Tier.D),
            new("The Ultimate Shadow Dragon", "Omega Shenron", Tier.F),
            new("Shocking Absorption Ability", "Buu (Super)", Tier.D),
            new("Ultimate Life Form with Immense Power", "Cell (1st Form)", Tier.D),
            new("Evil-Stained Androids", "Android 17 & Hell Fighter 17", Tier.F),
            new("Roar of Resentment", "Frieza (Full Power)", Tier.F),
            new("Fusion with the Big Gete Star", "Metal Cooler", Tier.F),
            new("Game of Death", "Androids 17 (Future) & 18 (Future)", Tier.E),
            new("Captain's Ace in the Hole", "Captain Ginyu", Tier.F),
            new("Epitome of Sublime Beauty", "Goku Black", Tier.F),
            new("Endless Evolution of the Warrior Race", "Super Saiyan Broly", Tier.F),
            new("Brief Paternal Moment", "Majin Vegeta", Tier.E),
            new("Unveiling of Power", "Raditz", Tier.F),
            new("Super Warrior Who Destroys All", "Legendary Super Saiyan Broly", Tier.S),

            // unreleased on global
            // new("Power of an Isolated Iron Wall", "Jiren", Tier.S),
            // new("Bright and Fun Life", "Master Roshi", Tier.A)
        ];
        #endregion

        #region Events
        public static IReadOnlyList<Event> Events { get => events; }

        private static List<Event> events =
        [
            new("Omega Shenron", Tier.C, "ShadowDragons", 8),
            new("Fighting Legend: Goku", Tier.F, "LGE"),
            new("Fighting Legend: Goku [GT Edition]", Tier.E, "LGT"),
            new("Fighting Legend: Vegeta", Tier.E, "LVE"),
            new("Fighting Legend: Frieza", Tier.D, "LFE"),
            new("The Devil Awakens", Tier.A, "DevilAwakens"),
            new("The Devil Awakens", Tier.A, "DevilAwakens", 2),
            new("The Devil Awakens", Tier.C, "DevilAwakens", 3),
            new("Supreme Magnificent Battle [Movie Edition]", Tier.C, "SMB_MOVIE", 3),
            new("Supreme Magnificent Battle [Movie Edition]", Tier.A, "SMB_MOVIE", 4),
            new("Supreme Magnificent Battle [Movie Edition]", Tier.A, "SMB_MOVIE", 5),
            new("Supreme Magnificent Battle [Movie Edition]", Tier.A, "SMB_MOVIE", 6),
            new("Supreme Magnificent Battle [Movie Edition]", Tier.A, "SMB_MOVIE", 7),
            new("Supreme Magnificent Battle [Universe Survival Saga]", Tier.S, "SMB_USS", 4),
            new("Supreme Magnificent Battle [Universe Survival Saga]", Tier.B, "SMB_USS"),
            new("Supreme Magnificent Battle [Dragon Ball Super Edition]", Tier.S, "SMB_DBS", 7),
            new("Supreme Magnificent Battle [Dragon Ball Super Edition]", Tier.S, "SMB_DBS", 8),
            new("Ultimate Red Zone [Majin Buu Saga]", Tier.S, "MBS_RZ", 4),
            new("Ultimate Red Zone [Majin Buu Saga]", Tier.A, "MBS_RZ", 2),
            new("Divine Wrath and Mortal Will", Tier.B, "DWMW", 9),
            new("Ultimate Red Zone [Movie Edition 2]", Tier.F, "MOVIE2_RZ"),
            new("Ultimate Red Zone [Movie Edition 2]", Tier.C, "MOVIE2_RZ", 6),
            new("Ultimate Red Zone [Movie Edition 2]", Tier.B, "MOVIE2_RZ", 7),
            new("9th Anniv.! Anniversary Battle", Tier.A, "ANNI", 9),
            new("Ultimate Red Zone [Dismal Future Edition]", Tier.B, "DF_RZ", 5),
            new("Ultimate Red Zone [Wicked Bloodline Edition]", Tier.C, "WB_RZ", 5),
            new("Ultimate Red Zone [Movie Edition]", Tier.B, "MOVIE_RZ", 8),
            new("Dragon Ball Z: Memorable Battles [Movie Edition]", Tier.A, "MB_ME", 7),
            new("Dragon Ball Z: Memorable Battles [Movie Edition]", Tier.A, "MB_ME", 8),

        ];
        #endregion

        #region Categories
        public static IReadOnlyList<Category> Categories { get => categories; }

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
            new("Gifted Warriors", Tier.B),
            new("Ginyu Force", Tier.E),
            new("Goku's Family", Tier.S),
            new("GT Bosses", Tier.D),
            new("Heavenly Events", Tier.E),
            new("Hybrid Saiyans", Tier.S),
            new("Inuman Deeds", Tier.D),
            new("Joined Forces", Tier.A),
            new("Kamehameha", Tier.S),
            new("Legendary Existence", Tier.C),
            new("Low-Class Warrior", Tier.D),
            new("Majin Buu Saga", Tier.S),
            new("Majin Power", Tier.S),
            new("Mastered Evolution", Tier.A),
            new("Miraculous Awakening", Tier.B),
            new("Movie Bosses", Tier.S),
            new("Movie Heroes", Tier.S),
            new("Namekians", Tier.F),
            new("Otherworld Warrior", Tier.F),
            new("Peppy Gals", Tier.E),
            new("Planet Namek Saga", Tier.F),
            new("Planetary Destruction", Tier.D),
            new("Potara", Tier.B),
            new("Power Absorption", Tier.B),
            new("Power Beyond Super Saiyan", Tier.S),
            new("Powerful Comeback", Tier.A),
            new("Power of Wishes", Tier.S),
            new("Pure Saiyans", Tier.S),
            new("Rapid Growth", Tier.E),
            new("Realm of Gods", Tier.A),
            new("Representatives of Universe 7", Tier.S),
            new("Resurrected Warriors", Tier.F),
            new("Revenge", Tier.D),
            new("Saiyan Saga", Tier.C),
            new("Saviors", Tier.E),
            new("Shadow Dragon Saga", Tier.F),
            new("Siblings' Bond", Tier.C),
            new("Space-Traveling Warriors", Tier.C),
            new("Special Pose", Tier.B),
            new("Storied Figures", Tier.D),
            new("Super Bosses", Tier.S),
            new("Super Heroes", Tier.S),
            new("Super Saiyan 2", Tier.E),
            new("Super Saiyan 3", Tier.B),
            new("Super Saiyans", Tier.S),
            new("Sworn Enemies", Tier.D),
            new("Target: Goku", Tier.D),
            new("Team Bardock", Tier.E),
            new("Terrifying Conquerors", Tier.C),
            new("Time Limit", Tier.A),
            new("Time Travelers", Tier.A),
            new("Tournament Participants", Tier.S),
            new("Transformation Boost", Tier.S),
            new("Turtle School", Tier.A),
            new("Uncontrollable Power", Tier.S),
            new("Universe 6", Tier.B),
            new("Universe 11", Tier.B),
            new("Universe Survival Saga", Tier.S),
            new("Vegeta's Family", Tier.A),
            new("Wicked Bloodline", Tier.D),
            new("World Tournament", Tier.C),
            new("Worldwide Chaos", Tier.C),
            new("Worthy Rivals", Tier.C),
            new("Youth", Tier.E)
        ];
        #endregion

        #region Internal
        public static string DATE_TAG => "date";
        public static string USER_NAME_TAG => "username";
        public static string DAILY_TYPE_TAG => "dailytype";
        public static string EVENT_TAG => "event";
        public static string CLEAR_TIME_TAG => "cleartime";
        public static string ITEMLESS_TAG => "itemless";
        #endregion

        #region Misc.
        private static readonly List<Unit> unitDB = [];
        public static IReadOnlyList<Unit> UnitDB { get => unitDB.AsReadOnly(); }

        public static readonly IReadOnlyDictionary<DokkanType, string> TypeToHexMap = new Dictionary<DokkanType, string>()
        {
            { DokkanType.AGL, "#0555D5" },
            { DokkanType.STR, "#C4151E" },
            { DokkanType.PHY, "#B46D00" },
            { DokkanType.INT, "#883BA9" },
            { DokkanType.TEQ, "#008806" }
        }.AsReadOnly();
        public static IReadOnlyList<DailyType> DailyTypes { get; }

        [GeneratedRegex("[^a-zA-Z0-9 -]")]
        public static partial Regex AlphaNumericRegex();
        #endregion


        static DDConstants()
        {
            LinkSkillMap = new Dictionary<string, LinkSkill>(linkSkills.Select(x => new KeyValuePair<string, LinkSkill>(x.Name, x)));
            DailyTypes = [DailyType.Category, DailyType.LinkSkill, DailyType.Character];
            unitDB = DDHelper.BuildCharacterDb().ToList();
        }

        public static Unit GetUnit(string name, string title)
        {
            return unitDB.FirstOrDefault(x => x.Name == name && x.Title == title);
        }

        public static Unit GetUnit(Leader leader)
        {
            return unitDB.FirstOrDefault(x => x.Name == leader.Name && x.Title == leader.Title);
        }
    }
}
