using DokkanDaily.Models.Enums;

namespace DokkanDaily.Models
{
    public class Challenge(DailyType dailyType, Stage todaysEvent, LinkSkill linkSkill, Category category, Leader leader, Unit todaysUnit, DateTime date)
    {
        public DailyType DailyType { get; set; } = dailyType;

        public Stage TodaysEvent { get; init; } = todaysEvent;

        public LinkSkill LinkSkill { get; init; } = linkSkill;

        public Category Category { get; init; } = category;

        public Leader Leader { get; init; } = leader;

        public Unit TodaysUnit { get; init; } = todaysUnit;

        private readonly DateTime _date = date.Date;

        public DateTime Date => _date;
    }
}
