using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class Category(string name, Tier d)
    {
        public string Name { get; init; } = name;

        public string ImageURL => $"images/categories/{string.Join('_', Name.Split(' '))}_Category.png";

        public Tier Tier { get; init; } = d;
    }
}
