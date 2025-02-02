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

        public string GetChallengeText(bool useDiscordFormatting = false)
        {
            string star = useDiscordFormatting ? "*" : "";
            string text = DailyType switch
            {
                DailyType.Character => $"{star}{Leader.FullName}{star} as the leader",
                DailyType.Category => $"only units belonging to the {star}{Category.Name}{star} category",
                DailyType.LinkSkill => $"only units with the link skill {star}{LinkSkill.Name}{star}",
                _ => throw new ArgumentException($"Unknown daily type '{DailyType}'")
            };
            return $"Defeat {star}{star}{TodaysEvent.FullName}{star}{star} using {text}";
        }
    }
}
