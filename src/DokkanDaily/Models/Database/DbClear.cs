using DokkanDaily.Repository.Attributes;

namespace DokkanDaily.Models.Database
{
    public class DbClear
    {
        [DataTableIndex(0)]
        public string DokkanNickname { get; init; }

        [DataTableIndex(1)]
        public string DiscordUsername { get; init; }

        [DataTableIndex(2)]
        public string DiscordId { get; init; }

        [DataTableIndex(3)]
        public bool ItemlessClear { get; init; }

        [DataTableIndex(4)]
        public string ClearTime { get; init; }

        [DataTableIndex(5)]
        public bool IsDailyHighscore { get; set; }

        public TimeSpan ClearTimeSpan { get; set; }
    }
}
