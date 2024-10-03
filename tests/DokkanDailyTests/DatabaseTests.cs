using Dapper;
using DokkanDaily.Configuration;
using DokkanDaily.Models.Database;
using DokkanDaily.Repository;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DokkanDailyTests
{
    [TestFixture]
    // THESE TESTS REQUIRE A DOCKER CONTAINER OF DokkanDailyDB TO BE LOCALLY DEPLOYED!
    // cd src/DokkanDailyDB
    // docker build . --build-arg PASSWORD="<YourStrong@Passw0rd>" -t mydatabase:1.0 --no-cache
    // docker run -p 1433:1433 --name sqldb -d mydatabase:1.0
    public class DatabaseTests
    {
        private DokkanDailyRepository repository;
        private SqlConnectionWrapper wrapper;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Mock<ILogger<DokkanDailyRepository>> mock = new(MockBehavior.Loose);
            wrapper = new();

            IOptions<DokkanDailySettings> options = Options.Create(new DokkanDailySettings() 
            { 
                SqlServerConnectionString = "Data Source=.,1433;Initial Catalog=mydatabase;Persist Security Info=True;User ID=SA;Password=<YourStrong@Passw0rd>;TrustServerCertificate=True;"
            });

            repository = new(wrapper, mock.Object, options);
        }

        [SetUp]
        public async Task Setup()
        {
            await wrapper.OpenAsync();
            await wrapper.ExecuteReaderAsync("delete from Core.StageClear; delete from Core.DokkanDailyUser;", new());
            await wrapper.CloseAsync();
        }

        [Test]
        public async Task BasicDbTest()
        {
            List<DbClear> dbClears = [];

            dbClears.AddRange(
            [
                new DbClear()
                {
                    DokkanNickname = "omni",
                    IsDailyHighscore = true,
                    ItemlessClear = true,
                    ClearTime = "n/a"
                },
				new DbClear()
                {
                    DokkanNickname = "rabs",
                    IsDailyHighscore = false,
                    ItemlessClear = true,
                    ClearTime = "n/a"
                },
                new DbClear()
                {
                    DokkanNickname = "owl",
                    IsDailyHighscore = false,
                    ItemlessClear = false,
                    ClearTime = "n/a"
                }
            ]);

            await repository.InsertDailyClears(dbClears);
            var result = await repository.GetDailyLeaderboard();

            Assert.That(result, Is.Not.Null);
            var list = result.ToList();
            Assert.That(list, Has.Count.EqualTo(3));

            await repository.InsertDailyClears(dbClears);
            await repository.InsertDailyClears(dbClears);
            result = await repository.GetDailyLeaderboard();

            list = result.ToList();
            list.Should().ContainEquivalentOf(new DbLeaderboardResult() 
            {
                DokkanNickname = "omni",
                DailyHighscores = 3,
                ItemlessClears = 3,
                TotalClears = 3
            });
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "rabs",
                DailyHighscores = 0,
                ItemlessClears = 3,
                TotalClears = 3
            });
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "owl",
                DailyHighscores = 0,
                ItemlessClears = 0,
                TotalClears = 3
            });

            await repository.InsertDailyClears([new DbClear()
            {
                DokkanNickname = "omni",
                DiscordUsername = "omni",
                IsDailyHighscore = true,
                ItemlessClear = true,
                ClearTime = "n/a"
            }]);
			result = await repository.GetDailyLeaderboard();
			list = result.ToList();
			list.Should().ContainEquivalentOf(new DbLeaderboardResult()
			{
				DokkanNickname = "omni",
                DiscordUsername = "omni",
				DailyHighscores = 4,
				ItemlessClears = 4,
				TotalClears = 4
			});
		}
    }
}
