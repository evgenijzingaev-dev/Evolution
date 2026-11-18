using Evolution.Core;
using Evolution.Volume;
using NUnit.Framework;
using System;
using System.IO;

namespace Evolution.Tests
{
    public sealed class LineageRecordsTests
    {
        string path;
        [SetUp]
        public void CreateTempPath() => path = Path.Combine(Path.GetTempPath(), "evolution-records-" + Guid.NewGuid(), "lineage-records.tsv");
        [TearDown]
        public void Cleanup() { if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path), true); }
        static LineageRecord Sample(long seed = 42) => new LineageRecord {
            Seed = seed, Name = "Искра", Generations = 4, Niches = 2, Divisions = 7, Hunts = 3, Era = true, End = 1, Seconds = 123.4 };
        [Test]
        public void AppendCreatesFileAndLoadRoundTripsEveryField()
        {
            Assert.IsEmpty(LineageRecords.Load(path), "No file yet.");
            LineageRecords.Append(path, Sample());
            var records = LineageRecords.Load(path);
            Assert.AreEqual(1, records.Count);
            var record = records[0];
            Assert.AreEqual(42, record.Seed);
            Assert.AreEqual("Искра", record.Name);
            Assert.AreEqual(4, record.Generations);
            Assert.AreEqual(2, record.Niches);
            Assert.AreEqual(7, record.Divisions);
            Assert.AreEqual(3, record.Hunts);
            Assert.IsTrue(record.Era);
            Assert.AreEqual(1, record.End);
            Assert.AreEqual(123.4, record.Seconds, .05);
        }
        [Test]
        public void AppendTrimsToTheLimitKeepingTheNewest()
        {
            for (int i = 0; i < LineageRecords.Limit + 5; i++) LineageRecords.Append(path, Sample(42));
            var records = LineageRecords.Load(path);
            Assert.AreEqual(LineageRecords.Limit, records.Count, "Old attempts make room for new ones.");
        }
        [Test]
        public void CorruptLinesAreSkippedWithoutBreakingTheFile()
        {
            LineageRecords.Append(path, Sample());
            File.AppendAllText(path, "garbage line\nv9\tbroken\n\n");
            LineageRecords.Append(path, Sample(7));
            var records = LineageRecords.Load(path);
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(42, records[0].Seed);
            Assert.AreEqual(7, records[1].Seed);
        }
        [Test]
        public void EncodeSanitizesTabAndNewlineInNames()
        {
            var encoded = LineageRecords.Encode(new LineageRecord { Name = "a\tb\nc", Seed = 1, End = 0 });
            Assert.AreEqual(9, encoded.Split('\t').Length - 1, "Exactly nine separators: the name stays one field.");
            Assert.IsFalse(encoded.Contains("\t\t"));
            Assert.IsFalse(encoded.Contains('\n'));
        }
        [Test]
        public void DifferentSeedsShareOneFileButStaySeparateRecords()
        {
            LineageRecords.Append(path, Sample(42));
            LineageRecords.Append(path, Sample(43));
            var records = LineageRecords.Load(path);
            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(42, records[0].Seed);
            Assert.AreEqual(43, records[1].Seed);
        }
    }
}
