using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Consoler;

public class TextTable
{
    public record struct ColumnInfo(string Name, int NameIndex, int StartIndex, int Length);

    List<ColumnInfo> _cols;
    string? _table;
    int _lineSize;

    public List<ColumnInfo> Columns => _cols;


    public TextTable(ReadOnlySpan<char> spanHeaderLine)
    {
        _cols = ConfigureRows(spanHeaderLine);
    }

    public List<string> ReadRow(ReadOnlySpan<char> spanLine)
    {
        var values = new List<string>();
        foreach (var col in _cols)
        {
            var value = spanLine.Slice(col.StartIndex, col.Length).TrimEnd().ToString();
            values.Add(value);
        }
        return values;

    }

    public List<ColumnInfo> ConfigureRows(ReadOnlySpan<char> spanHeaderLine)
    {
        if (spanHeaderLine.IsEmpty)
        {
            throw new ArgumentException("Header line is empty", nameof(spanHeaderLine));
        }


        List<ColumnInfo> cols = [];
        int colIndex;
        int count = 0;
        int spaceIndex;
        int nextInfoIndex;

        while (count < spanHeaderLine.Length)
        {
            colIndex = count;
            spaceIndex = spanHeaderLine[count..].IndexOf(' ');
            if (spaceIndex < 0)
            {
                spaceIndex = spanHeaderLine.Length;
            }
            else
            {
                spaceIndex += count;
            }
            var colName = spanHeaderLine[colIndex..spaceIndex].ToString();
            nextInfoIndex = spanHeaderLine[spaceIndex..].IndexOfAnyExcept(' ');
            if (nextInfoIndex < 0)
            {
                spaceIndex = spanHeaderLine.Length-1;
            }
            else
            {
                spaceIndex += nextInfoIndex - 1;
            }
            cols.Add(new ColumnInfo(colName, colIndex, colIndex, spaceIndex - colIndex));
            count = spaceIndex + 1;
        }

        return cols;
    }

    public List<ColumnInfo> ConfigureRows(ReadOnlySpan<char> spanHeaderLine, List<string> headers)
    {
        if (spanHeaderLine.IsEmpty)
        {
            throw new ArgumentException("Header line is empty", nameof(spanHeaderLine));
        }
        if (headers.AnySafe())
        {
            throw new ArgumentException("Headers list is empty", nameof(headers));
        }
        if (headers.Any(h => h.IsEmpty()))
        {
            throw new ArgumentException("Headers has empty entries", nameof(headers));
        }
        if (headers.Distinct().Count() != headers.Count())
        {
            throw new ArgumentException("Headers has duplicate entries", nameof(headers));
        }

        List<ColumnInfo> cols = [];

        foreach (var header in headers)
        {
            var index = spanHeaderLine.IndexOf(header.AsSpan(), StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                cols.Add(new ColumnInfo(header, index, 0, 0));
            }
        }

        return cols;
    }
}
