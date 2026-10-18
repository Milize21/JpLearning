using System.Text;

namespace Nomax.SharedKernel.Text;

/// <summary>
/// Minimal RFC 4180 reader: quoted fields, escaped quotes, line breaks inside quotes, optional BOM.
/// The delimiter is ',' unless the header uses ';' (what spreadsheet apps in the Indonesian locale export).
/// </summary>
public static class CsvReader
{
    public static IReadOnlyList<IReadOnlyList<string>> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 0 && text[0] == '﻿')
        {
            text = text[1..];
        }

        var delimiter = DetectDelimiter(text);
        var records = new List<IReadOnlyList<string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                EndRecord();
            }
            else
            {
                field.Append(c);
            }
        }

        EndRecord();
        return records;

        void EndRecord()
        {
            fields.Add(field.ToString());
            field.Clear();
            if (fields.Any(value => value.Trim().Length > 0))
            {
                records.Add([.. fields]);
            }

            fields.Clear();
        }
    }

    private static char DetectDelimiter(string text)
    {
        var lineEnd = text.IndexOfAny(['\r', '\n']);
        var header = lineEnd < 0 ? text : text[..lineEnd];
        return header.Contains(';', StringComparison.Ordinal) && !header.Contains(',', StringComparison.Ordinal) ? ';' : ',';
    }
}
