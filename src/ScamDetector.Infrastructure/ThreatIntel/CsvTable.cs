using System.Text;

namespace ScamDetector.Infrastructure.ThreatIntel;

public static class CsvTable
{
    private const char Quote = '"';
    private const char Separator = ',';

    public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string content)
    {
        var records = ParseRecords(content.TrimStart('﻿'));
        if (records.Count == 0)
            return [];

        var header = records[0];
        return records.Skip(1)
            .Where(record => record.Count > 1 || record[0].Length > 0)
            .Select(record => (IReadOnlyDictionary<string, string>)header
                .Select((name, index) => (name, value: index < record.Count ? record[index] : string.Empty))
                .ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal))
            .ToList();
    }

    private static List<List<string>> ParseRecords(string content)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (inQuotes)
            {
                if (character == Quote && index + 1 < content.Length && content[index + 1] == Quote)
                {
                    field.Append(Quote);
                    index++;
                }
                else if (character == Quote)
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }

            switch (character)
            {
                case Quote:
                    inQuotes = true;
                    break;
                case Separator:
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = [];
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}
