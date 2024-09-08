namespace DokkanDaily.Models
{
    public class LinkSkill(string name, Difficulty d)
    {
        public string Name { get; init; } = name;

        public Difficulty Difficulty { get; init; } = d;
    }
}
