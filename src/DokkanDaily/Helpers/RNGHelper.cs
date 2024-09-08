namespace DokkanDaily.Helpers
{
    public static class RNGHelper
    {
        public static Random GetDailySeededRandom()
        {
            var date = DateTime.UtcNow.Date;
            var seed = date.Year * 1000 + date.DayOfYear;
            return new Random(seed);
        }
    }
}
