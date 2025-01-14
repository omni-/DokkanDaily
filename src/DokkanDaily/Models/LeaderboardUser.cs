namespace DokkanDaily.Models
{
    public class LeaderboardUser
    {
        public string DokkanNickname { get; init; }

        public string DiscordUsername { get; init; }

        public int TotalScore { get; init; }

        public int TotalHighscores { get; init; }

        public int ItemlessClears { get; init; }
    }
}
