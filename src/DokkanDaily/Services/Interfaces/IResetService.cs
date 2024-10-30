namespace DokkanDaily.Services.Interfaces
{
    public interface IResetService
    {
        Task DoReset(int daysAgo = 0, bool isAdhoc = false);
    }
}
