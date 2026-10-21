using System;

namespace CodeScope.ViewModels;

/// <summary>
/// How one table of the report is sorted: the column and the direction. The
/// report keeps this for every table, so the arrow in a column heading and
/// the order of the rows can never disagree.
///
/// The heading is what the user sees ("Lines changed"); the key is the
/// property the rows are ordered by ("LinesChanged"). Keeping both means a
/// heading that appears in two tables — "File" is in three of them — still
/// points at the right column.
/// </summary>
public sealed class TableSortState
{
    public TableSortState(string table, string heading, string key, bool descending)
    {
        Table = table;
        Heading = heading;
        Key = key;
        Descending = descending;
    }

    /// <summary>Which table this state belongs to ("Files", "Hotspots", …).</summary>
    public string Table { get; }

    /// <summary>The heading of the column the table is sorted by.</summary>
    public string Heading { get; private set; }

    /// <summary>The property the rows are ordered by.</summary>
    public string Key { get; private set; }

    /// <summary>True when the rows run from the largest value down.</summary>
    public bool Descending { get; private set; }

    /// <summary>Records a new sort.</summary>
    public void Set(string heading, string key, bool descending)
    {
        Heading = heading;
        Key = key;
        Descending = descending;
    }
}
