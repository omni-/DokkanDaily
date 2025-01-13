namespace DokkanDaily.Models
{
    public class TempCache<T>(T value)
    {
        public T Value { get; set; } = value;

        private DateTime CacheDate { get; } = DateTime.UtcNow;

        public bool CacheExpired => CacheDate.Date != DateTime.UtcNow.Date;
    }
}
