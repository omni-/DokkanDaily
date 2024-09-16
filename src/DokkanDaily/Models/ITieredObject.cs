using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public interface ITieredObject
    {
        public Tier Tier { get; init; }
    }
}
