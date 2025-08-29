using Dapper;
using DokkanDaily.Configuration;
using DokkanDaily.Models;
using DokkanDaily.Models.Database;
using DokkanDaily.Models.Enums;
using DokkanDaily.Repository;
using FluentAssertions;
using Microsoft.Data.SqlClient;
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
        private SqlConnection conn;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Mock<ILogger<DokkanDailyRepository>> mock = new(MockBehavior.Loose);
            string sqlServerConnectionString = "Data Source=.,1433;Initial Catalog=mydatabase;Persist Security Info=True;User ID=SA;Password=<YourStrong@Passw0rd>;TrustServerCertificate=True;";
            conn = new(sqlServerConnectionString);

            // todo: docker stuff here

            IOptions<DokkanDailySettings> options = Options.Create(new DokkanDailySettings()
            {
                SqlServerConnectionString = sqlServerConnectionString

            });

            repository = new(mock.Object, options);
        }

        [SetUp]
        public async Task Setup()
        {
            await conn.OpenAsync();
            await conn.ExecuteReaderAsync("delete from Core.StageClear; delete from Core.DokkanDailyUser; delete from Core.DailyChallenge;", new());
            await conn.CloseAsync();
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
                    DiscordId = "112089455933792256",
                    IsDailyHighscore = true,
                    ItemlessClear = true,
                    ClearTime = "n/a"
                }
            ]);
            await repository.InsertDailyClears(dbClears, dt);
            var result = await repository.GetLeaderboardByDate(dt);

            Assert.That(result, Is.Not.Null, "the repository should return a leaderboard");
            var list = result.ToList();
            Assert.That(list, Has.Count.EqualTo(2), "the returned leaderboard should have the correct number of elements");
            Assert.That(list.Any(x => x.DiscordId == "112089455933792256"), Is.True, "an element should contain a discord id");
        }

        [Test]
        public async Task DatabaseCanRecordAndReturnChallengeList()
        {
            await repository.InsertChallenge(new(DailyType.Character, new Stage("foo", Tier.F, "fakepath"), new("a", Tier.F), new("b", Tier.F), new("bar", "baz", Tier.F), null, DateTime.UtcNow));
            var result = await repository.GetChallengeList(DateTime.UtcNow - TimeSpan.FromDays(2));
            var match = result.FirstOrDefault(x => x.DailyTypeName == "Character" && x.Category == null && x.Stage == 1 && x.Event == "foo" && x.LeaderFullName == "[bar] baz" && x.LinkSkill == null);
            Assert.That(match, Is.Not.Null, "Could not find matching record in db result");

            result = await repository.GetChallengeList(null);
            match = result.FirstOrDefault(x => x.DailyTypeName == "Character" && x.Category == null && x.Stage == 1 && x.Event == "foo" && x.LeaderFullName == "[bar] baz" && x.LinkSkill == null);
            Assert.That(match, Is.Not.Null, "Could not find matching record in db result");


            result = await repository.GetChallengeList(DateTime.UtcNow);
            Assert.That(result, Is.Empty, "Returned a row when none should be returned");
        }

        [Test]
        public async Task DatabaseCanGetHallOfFame()
        {
            List<DbClear> dbClears = [];

            DateTime dt = new(2024, 12, 25);
            DateTime now = DateTime.UtcNow;

            dbClears.Add(new DbClear()
            {
                DokkanNickname = "old clear",
                IsDailyHighscore = true,
                ItemlessClear = true,
                ClearTime = "n/a"
            });

            await repository.InsertDailyClears(dbClears, dt);
            await repository.InsertDailyClears([
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
            ], now);
            var result = await repository.GetHallOfFame();

            Assert.That(result, Is.Not.Null, "the repository should return a HoF");
            var list = result.ToList();
            Assert.That(list, Has.Count.EqualTo(1), "the returned HoF should have the correct number of elements");

            await repository.InsertDailyClears(dbClears, dt.AddMonths(-2));
            await repository.InsertDailyClears(dbClears, dt.AddMonths(-3));
            result = await repository.GetHallOfFame();

            list = result.ToList();

            list.Should().ContainEquivalentOf(new DbLeaderboardResult()
            {
                DokkanNickname = "old clear",
                DailyHighscores = 3,
                ItemlessClears = 3,
                TotalClears = 3
            }, "the hall of fame should record 3 clears for 'old clear'");
        }

        [Test]
        public async Task TestAllDatabaseFunctions()
        {
            List<DbClear> dbClears = [];

            DateTime dt = DateTime.Parse("01/15/2025");


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
            await repository.InsertDailyClears([new DbClear()
            {
                DokkanNickname = "old clear",
                IsDailyHighscore = true,
                ItemlessClear = true,
                ClearTime = "n/a"
            }], dt.AddMonths(-1));
            var result = await repository.GetLeaderboardByDate(dt);

            Assert.That(result, Is.Not.Null, "the repository should return a leaderboard");
            var list = result.ToList();
            Assert.That(list, Has.Count.EqualTo(3), "the returned leaderboard should have the correct number of elements");

            await repository.InsertDailyClears(dbClears, dt + TimeSpan.FromDays(1));
            await repository.InsertDailyClears(dbClears, dt + TimeSpan.FromDays(2));
            result = await repository.GetLeaderboardByDate(dt);

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
            result = await repository.GetLeaderboardByDate(dt);
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
