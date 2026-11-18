using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Evolution.Volume
{
    public struct LineageRecord
    {
        public long Seed;
        public string Name;
        public int Generations, Niches, Divisions, Hunts, End;
        public bool Era;
        public double Seconds;
    }
    // Local-only history of finished dynasties, stored next to the region archive of
    // the same seed. One tab-separated line per record; a deterministic world on the
    // same seed makes beating your own previous line a real, comparable goal.
    // No cloud, no account, no invented ranking.
    public static class LineageRecords
    {
        public const int Limit = 40;
        public static void Append(string path, in LineageRecord record)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var records = Load(path);
            records.Add(record);
            if (records.Count > Limit) records.RemoveRange(0, records.Count - Limit);
            var builder = new StringBuilder();
            foreach (var line in records) builder.AppendLine(Encode(line));
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }
        public static List<LineageRecord> Load(string path)
        {
            var records = new List<LineageRecord>();
            try {
                if (!File.Exists(path)) return records;
                foreach (var line in File.ReadAllLines(path))
                    if (TryDecode(line, out var record)) records.Add(record);
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return records;
        }
        public static string Encode(in LineageRecord record)
        {
            // Names come from the fixed catalog; still sanitize defensively.
            string name = (record.Name ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
            return string.Join("\t", "v1", record.Seed.ToString(CultureInfo.InvariantCulture), name,
                record.Generations.ToString(CultureInfo.InvariantCulture),
                record.Niches.ToString(CultureInfo.InvariantCulture),
                record.Divisions.ToString(CultureInfo.InvariantCulture),
                record.Hunts.ToString(CultureInfo.InvariantCulture),
                record.Era ? "1" : "0",
                record.End.ToString(CultureInfo.InvariantCulture),
                record.Seconds.ToString("F1", CultureInfo.InvariantCulture));
        }
        public static bool TryDecode(string line, out LineageRecord record)
        {
            record = default;
            if (string.IsNullOrEmpty(line)) return false;
            var parts = line.Split('\t');
            if (parts.Length != 10 || parts[0] != "v1") return false;
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Seed)) return false;
            record.Name = parts[2];
            if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Generations)) return false;
            if (!int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Niches)) return false;
            if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Divisions)) return false;
            if (!int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Hunts)) return false;
            if (parts[7] != "0" && parts[7] != "1") return false;
            record.Era = parts[7] == "1";
            if (!int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out record.End)) return false;
            if (!double.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out record.Seconds)) return false;
            return true;
        }
    }
}
