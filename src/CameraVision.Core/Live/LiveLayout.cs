using System.Globalization;

namespace CameraVision.Core.Live;

/// <summary>One camera cell of a live grid layout (1-based CSS grid lines).</summary>
public sealed record LiveSlot(int Column, int Row, int ColumnSpan = 1, int RowSpan = 1)
{
    /// <summary>Inline CSS placing the cell on the grid.</summary>
    public string GridArea =>
        $"grid-column: {Column} / span {ColumnSpan}; grid-row: {Row} / span {RowSpan};";
}

/// <summary>
/// A named arrangement of camera cells on a CSS grid. Column/row weights are
/// <c>fr</c> units; wide/tall "highlight" cells use 2:1 or 3:1 weights so that
/// every cell keeps the same aspect ratio as the small ones.
/// </summary>
public sealed record LiveLayout(
    string Key,
    string Name,
    IReadOnlyList<double> Columns,
    IReadOnlyList<double> Rows,
    IReadOnlyList<LiveSlot> Slots)
{
    public int CameraCount => Slots.Count;

    public string GridTemplateColumns => Template(Columns);
    public string GridTemplateRows => Template(Rows);

    /// <summary>Inline CSS for the grid container.</summary>
    public string GridStyle =>
        $"grid-template-columns: {GridTemplateColumns}; grid-template-rows: {GridTemplateRows};";

    /// <summary>
    /// Width:height of the whole grid when every cell is 16:9 — lets the page
    /// size the stage so no cell is letterboxed.
    /// </summary>
    public (double Width, double Height) AspectRatio =>
        (Columns.Sum() * 16, Rows.Sum() * 9);

    private static string Template(IReadOnlyList<double> weights) =>
        string.Join(' ', weights.Select(w => w.ToString(CultureInfo.InvariantCulture) + "fr"));
}
