using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class LinkSkill(string name, Tier d) : ITieredObject
    {
        public string Name { get; init; } = name;

        public Tier Tier { get; init; } = d;
    }
}
