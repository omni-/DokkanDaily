namespace DokkanDaily.Models
{
    public class LinkSkill(string name, Tier d)
    {
        public string Name { get; init; } = name;

        public Tier Tier { get; init; } = d;
    }
}
