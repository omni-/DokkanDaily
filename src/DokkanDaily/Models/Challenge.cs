using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class Challenge(DailyType dailyType, Event todaysEvent, LinkSkill linkSkill, Category category, Leader leader, Unit todaysUnit)
    {
        public DailyType DailyType { get; init; } = dailyType;

        public Event TodaysEvent { get; init; } = todaysEvent;

        public LinkSkill LinkSkill { get; init; } = linkSkill;

        public Category Category { get; init; } = category;

        public Leader Leader { get; init; } = leader;

        public Unit TodaysUnit { get; init; } = todaysUnit;
    }
}
