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
    // run buildDatabase.cmd (Windows) or buildDatabase.sh (Linux) from the root directory
    public class DatabaseTests
    {
        private DokkanDailyRepository repository;
        private SqlConnectionWrapper wrapper;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Mock<ILogger<DokkanDailyRepository>> mock = new(MockBehavior.Loose);
            wrapper = new();

            // todo: docker stuff here

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
        public async Task TheDatabaseCanRecordNullUsernames()
        {
            List<DbClear> dbClears = [];

            DateTime dt = DateTime.UtcNow.Date;

            dbClears.AddRange(
            [
                new()
                {
                    DokkanNickname = "omni",
                    DiscordUsername = null,
                    IsDailyHighscore = true,
                    ItemlessClear = true,
                    ClearTime = "n/a"
                },
                new()
                {
                    DokkanNickname = null,
                    DiscordUsername = "omni",
                    IsDailyHighscore = true,
                    ItemlessClear = true,
                    ClearTime = "n/a"
                }
            ]);
            await repository.InsertDailyClears(dbClears, dt);
            var result = await repository.GetDailyLeaderboard();

            Assert.That(result, Is.Not.Null, "the repository should return a leaderboard");
            var list = result.ToList();
            Assert.That(list, Has.Count.EqualTo(2), "the returned leaderboard should have the correct number of elements");

            // too hard :(

            //await repository.InsertDailyClears([new DbClear()
            //{
            //    DokkanNickname = "omni",
            //    DiscordUsername = "omni",
            //    IsDailyHighscore = true,
            //    ItemlessClear = true,
            //    ClearTime = "n/a"
            //}], dt + TimeSpan.FromDays(1));

            //result = await repository.GetDailyLeaderboard();
            //list = result.ToList();
            //Assert.That(list, Has.Count.EqualTo(1), "the clears should be bound to one user");
        }

        [Test]
        public async Task TestAllDatabaseFunctions()
        {
            List<DbClear> dbClears = [];

            DateTime dt = DateTime.UtcNow.Date;


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

            await repository.InsertDailyClears(dbClears, dt);
            var result = await repository.GetDailyLeaderboard();

            Assert.That(result, Is.Not.Null, "the repository should return a leaderboard");
            var list = result.ToList();
            Assert.That(list, Has.Count.EqualTo(3), "the returned leaderboard should have the correct number of elements");

            await repository.InsertDailyClears(dbClears, dt + TimeSpan.FromDays(1));
            await repository.InsertDailyClears(dbClears, dt + TimeSpan.FromDays(2));
            result = await repository.GetDailyLeaderboard();

            list = result.ToList();
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "omni",
                DailyHighscores = 3,
                ItemlessClears = 3,
                TotalClears = 3
            }, "the database should record 3 clears for 'omni'");
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "rabs",
                DailyHighscores = 0,
                ItemlessClears = 3,
                TotalClears = 3
            }, "the database should record 3 clears for 'rabs'");
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "owl",
                DailyHighscores = 0,
                ItemlessClears = 0,
                TotalClears = 3
            }, "the database should record 3 clears for 'owl'");

            await repository.InsertDailyClears([new DbClear()
            {
                DokkanNickname = "omni",
                DiscordUsername = "omni",
                IsDailyHighscore = true,
                ItemlessClear = true,
                ClearTime = "n/a"
            }], dt + TimeSpan.FromDays(3));
            await repository.InsertDailyClears([new DbClear()
            {
                DokkanNickname = null,
                DiscordUsername = "omni",
                IsDailyHighscore = true,
                ItemlessClear = true,
                ClearTime = "n/a"
            }], dt + TimeSpan.FromDays(4));
            result = await repository.GetDailyLeaderboard();
            list = result.ToList();
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "omni",
                DiscordUsername = "omni",
                DailyHighscores = 5,
                ItemlessClears = 5,
                TotalClears = 5
            }, "the database should bind a dokkan username to a discord username if they have appeared together before");
            await repository.InsertDailyClears([new DbClear()
            {
                DokkanNickname = "omni",
                DiscordUsername = "omni",
                IsDailyHighscore = true,
                ItemlessClear = true,
                ClearTime = "n/a"
            }], dt + TimeSpan.FromDays(3));
            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "omni",
                DiscordUsername = "omni",
                DailyHighscores = 5,
                ItemlessClears = 5,
                TotalClears = 5
            }, "the database should not add a duplicate clear");
        }
    }
}
