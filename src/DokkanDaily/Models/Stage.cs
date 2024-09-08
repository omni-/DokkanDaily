namespace DokkanDaily.Models
{
    public class Stage(string name, Difficulty d)
    {
        public string Name { get; init; } = name;

        public Difficulty Difficulty { get; init; } = d;

        public string ImagePath { get; set; }
    }
}
