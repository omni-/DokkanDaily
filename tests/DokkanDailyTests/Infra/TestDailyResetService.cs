using DokkanDaily.Repository;
using DokkanDaily.Services;
using Microsoft.Extensions.Logging;

namespace DokkanDailyTests.Infra
{
    internal class TestDailyResetService(
        IAzureBlobService azureBlobService, 
        IDokkanDailyRepository repository, 
        ILogger<DailyResetService> logger) 
    : DailyResetService(azureBlobService, repository, logger)
    {
        protected override Task WaitUntilNextScheduledTime(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
