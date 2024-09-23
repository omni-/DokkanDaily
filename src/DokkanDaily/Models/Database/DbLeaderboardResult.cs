namespace DokkanDaily.Models.Database
{
    public class DbLeaderboardResult
    {
        public string DokkanNickname { get; init; }

        public string DiscordUsername { get; init; }

        public int ItemlessClears { get; init; }

        public int TotalClears { get; init; }

        public int DailyHighscores { get; init; }
    }
}
