namespace DokkanDaily.Models.Database
{
    public class DbChallenge
    {
        public string Event { get; init; }

        public int Stage { get; init; }

        public DateTime Date { get; init; }

        public string LeaderFullName { get; init; }

        public string Category { get; init; }

        public string LinkSkill { get; init; }

        public string DailyTypeName { get; init; }
    }
}
