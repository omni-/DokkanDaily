using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class Unit
    {
        public string Name { get; init; }
        public string Title { get; init; }
        public int? MaxLevel { get; init; }
        public int? MaxSALevel { get; init; }
        public string Rarity { get; init; }
        public string Class { get; init; }
        public string Type { get; init; }
        public DokkanType DokkanType => Enum.Parse<DokkanType>(Type);
        public int? Cost { get; init; }
        public string Id { get; init; }
        public string ImageURL { get; set; }
        public string LeaderSkill { get; init; }
        public string EzaLeaderSkill { get; init; }
        public string SuperAttack { get; init; }
        public string EzaSuperAttack { get; init; }
        public string UltraSuperAttack { get; init; }
        public string EzaUltraSuperAttack { get; init; }
        public string Passive { get; init; }
        public string EzaPassive { get; init; }
        public List<string> Links { get; } = new List<string>();
        public List<string> Categories { get; } = new List<string>();
        public List<string> KiMeter { get; } = new List<string>();
        public int? BaseHP { get; init; }
        public int? MaxLevelHP { get; init; }
        public int? FreeDupeHP { get; init; }
        public int? RainbowHP { get; init; }
        public int? BaseAttack { get; init; }
        public int? MaxLevelAttack { get; init; }
        public int? FreeDupeAttack { get; init; }
        public int? RainbowAttack { get; init; }
        public int? BaseDefence { get; init; }
        public int? MaxDefence { get; init; }
        public int? FreeDupeDefence { get; init; }
        public int? RainbowDefence { get; init; }
        public string KiMultiplier { get; init; }
        public List<object> Transformations { get; } = new List<object>();
    }
}
