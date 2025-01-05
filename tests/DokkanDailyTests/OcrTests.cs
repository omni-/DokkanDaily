#nullable enable

using DokkanDaily.Models;
using DokkanDaily.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework.Internal;
using System.Collections.Concurrent;

namespace DokkanDailyTests
{
    [TestFixture]
    public class OcrTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
            {
                Assert.Ignore("Skipping OCR tests as they currently can't run on remote");
            }
        }

        private static string GetDataDirectory()
        {
            string workingDirectory = Directory.GetCurrentDirectory();
            string sourceDirectory = Path.GetFullPath(Path.Combine(workingDirectory, "../../.."));
            return Path.Combine(sourceDirectory, "Data");
        }

        private class SnapshotData
        {
            public string[] Categories { get; set; } = [];

            public string? Nickname { get; set; }
            public string? ClearTime { get; set; }
            public bool? ItemlessClear { get; set; }
            public string? OriginalFilename { get; set; }
        }

        private static IEnumerable<TestCaseData> GetImageTestCases()
        {
            string[] imageExtensions = new[] { ".png", ".jpg", ".jpeg" };
            foreach (string imagePath in Directory
                .EnumerateFiles(GetDataDirectory(), "*", SearchOption.AllDirectories)
                .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLower()))
            )
            {
                TestCaseData testCase = new TestCaseData(imagePath)
                    .SetName(Path.GetFileNameWithoutExtension(imagePath))
                    .SetProperty("filePath", imagePath);

                SnapshotData? snapshot = SnapshotHelper<SnapshotData>.LoadSnapshot(imagePath);
                if (snapshot != null)
                {
                    foreach (string category in snapshot.Categories)
                    {
                        testCase.SetCategory(category);
                    }

                    if (snapshot.Categories.Contains("lang_jpn"))
                    {
                        // fixme targeted OCR not working for Japanese yet
                        continue;
                    }
                }


                yield return testCase;
            }
        }

        private static class SnapshotHelper<T> where T : class
        {
            public static void SaveSnapshot(string imagePath, T data)
            {
                string snapshotPath = GetSnapshotPath(imagePath);
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(snapshotPath, json);
            }

            public static T? LoadSnapshot(string imagePath)
            {
                string snapshotPath = GetSnapshotPath(imagePath);
                if (!File.Exists(snapshotPath))
                {
                    return null;
                }

                string json = File.ReadAllText(snapshotPath);
                return JsonConvert.DeserializeObject<T>(json);
            }

            private static string GetSnapshotPath(string imagePath)
            {
                string directory = Path.GetDirectoryName(imagePath) ?? "";
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imagePath);
                return Path.Combine(directory, $"{fileNameWithoutExtension}.json");
            }
        }

        private ClearMetadata? ProcessImage(string imagePath)
        {
            Mock<ILogger<OcrService>> loggerMock = new();
            OcrService service = new(loggerMock.Object, Options.Create(new DokkanDaily.Configuration.DokkanDailySettings() { FeatureFlags = new() { EnableJapaneseParsing = true } }), new OcrFormatProvider());
            MemoryStream ms = new();
            File.OpenRead(imagePath).CopyTo(ms);
            ClearMetadata? result = service.ProcessImage(ms);
            return result;
        }

        [Test]
        [Explicit]
        [Description("Generates a snapshot file for all test cases that do not have one")]
        public void GenerateSnapshots()
        {
            foreach (TestCaseData testCase in GetImageTestCases())
            {
                string? imagePath = testCase.Properties.Get("filePath") as string;
                if (imagePath == null)
                {
                    throw new Exception("Test case does not have an image path");
                }

                SnapshotData? snapshot = SnapshotHelper<SnapshotData>.LoadSnapshot(imagePath);
                if (snapshot == null)
                {
                    ClearMetadata? result = ProcessImage(imagePath);

                    snapshot = new SnapshotData
                    {
                        Nickname = result?.Nickname,
                        ClearTime = result?.ClearTime,
                        ItemlessClear = result?.ItemlessClear
                    };
                }
                SnapshotHelper<SnapshotData>.SaveSnapshot(imagePath, snapshot);
            }
        }

        private static readonly ConcurrentDictionary<string, Lazy<ClearMetadata?>> ProcessedResultsCache = new();

        private ClearMetadata? GetProcessedResult(string imagePath)
        {
            return ProcessedResultsCache
                .GetOrAdd(imagePath, key => new Lazy<ClearMetadata?>(() => ProcessImage(key)))
                .Value;
        }

        // [TestCaseSource(nameof(GetImageTestCases))]
        // [Parallelizable(ParallelScope.Children)]
        // public void BasicOcrTest(string imagePath)
        // {
        //     SnapshotData? snapshot = SnapshotHelper<SnapshotData>.LoadSnapshot(imagePath);
        //     if (snapshot == null)
        //     {
        //         Assert.Ignore("Snapshot not found");
        //     }
        //
        //     ClearMetadata? result = GetProcessedResult(imagePath);
        //
        //     Assert.Multiple(() =>
        //     {
        //         Assert.That(result?.ItemlessClear, Is.EqualTo(snapshot.ItemlessClear));
        //         Assert.That(result?.Nickname, Is.EqualTo(snapshot.Nickname));
        //         Assert.That(result?.ClearTime, Is.EqualTo(snapshot.ClearTime));
        //     });
        // }

        [TestCaseSource(nameof(GetImageTestCases))]
        [Parallelizable(ParallelScope.Children)]
        public void Nickname(string imagePath)
        {
            SnapshotData? snapshot = SnapshotHelper<SnapshotData>.LoadSnapshot(imagePath);
            if (snapshot == null)
            {
                Assert.Ignore("Snapshot not found");
            }

            ClearMetadata? result = GetProcessedResult(imagePath);

            Assert.That(result?.Nickname, Is.EqualTo(snapshot.Nickname));
        }

        [TestCaseSource(nameof(GetImageTestCases))]
        [Parallelizable(ParallelScope.Children)]
        public void ClearTime(string imagePath)
        {
            SnapshotData? snapshot = SnapshotHelper<SnapshotData>.LoadSnapshot(imagePath);
            if (snapshot == null)
            {
                Assert.Ignore("Snapshot not found");
            }

            ClearMetadata? result = GetProcessedResult(imagePath);

            Assert.That(result?.ClearTime, Is.EqualTo(snapshot.ClearTime));
        }

        [TestCaseSource(nameof(GetImageTestCases))]
        [Parallelizable(ParallelScope.Children)]
        public void Itemless(string imagePath)
        {
            SnapshotData? snapshot = SnapshotHelper<SnapshotData>.LoadSnapshot(imagePath);
            if (snapshot == null)
            {
                Assert.Ignore("Snapshot not found");
            }

            ClearMetadata? result = GetProcessedResult(imagePath);

            Assert.That(result?.ItemlessClear, Is.EqualTo(snapshot.ItemlessClear));
        }
    }
}
