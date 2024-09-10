using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class Category(string name, Tier d) : ITieredObject
    {
        public string Name { get; init; } = name;

        public string ImageURL => $"images/categories/{string.Join('_', Name.Replace(":", "").Replace("/", "-").Split(' '))}_Category.png";

        public Tier Tier { get; init; } = d;
    }
}
