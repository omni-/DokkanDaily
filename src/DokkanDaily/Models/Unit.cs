namespace DokkanDaily.Models
{
    public class Unit(string title, string name, Tier t)
    {
        public string Name { get; init; } = name;

        public string Title { get; init; } = title;

        public Tier Tier { get; init; } = t;
    }
}
