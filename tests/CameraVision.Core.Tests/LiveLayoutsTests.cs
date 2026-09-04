using CameraVision.Core.Live;

namespace CameraVision.Core.Tests;

public class LiveLayoutsTests
{
    [Fact]
    public void Keys_are_unique_and_every_count_has_templates()
    {
        Assert.Equal(LiveLayouts.All.Count, LiveLayouts.All.Select(l => l.Key).Distinct().Count());
        for (var n = 1; n <= LiveLayouts.MaxCameras; n++)
            Assert.NotEmpty(LiveLayouts.For(n));
    }

    [Fact]
    public void Slots_fit_the_grid_without_overlapping_and_cover_every_cell()
    {
        foreach (var layout in LiveLayouts.All)
        {
            var covered = new HashSet<(int, int)>();
            foreach (var slot in layout.Slots)
            {
                Assert.True(slot.Column >= 1 && slot.Column + slot.ColumnSpan - 1 <= layout.Columns.Count, layout.Key);
                Assert.True(slot.Row >= 1 && slot.Row + slot.RowSpan - 1 <= layout.Rows.Count, layout.Key);
                for (var c = slot.Column; c < slot.Column + slot.ColumnSpan; c++)
                    for (var r = slot.Row; r < slot.Row + slot.RowSpan; r++)
                        Assert.True(covered.Add((c, r)), $"{layout.Key}: cell {c},{r} used twice");
            }
            Assert.Equal(layout.Columns.Count * layout.Rows.Count, covered.Count);
        }
    }

    [Fact]
    public void Grid_style_uses_invariant_fr_units()
    {
        var layout = LiveLayouts.Find("3-main-left")!;
        Assert.Equal("2fr 1fr", layout.GridTemplateColumns);
        Assert.Equal("1fr 1fr", layout.GridTemplateRows);
        Assert.Equal("grid-column: 1 / span 1; grid-row: 1 / span 2;", layout.Slots[0].GridArea);
        Assert.Equal((48, 18), layout.AspectRatio);
    }

    [Fact]
    public void Default_clamps_to_the_supported_range()
    {
        Assert.Equal(1, LiveLayouts.Default(0).CameraCount);
        Assert.Equal(LiveLayouts.MaxCameras, LiveLayouts.Default(99).CameraCount);
        Assert.Null(LiveLayouts.Find("nope"));
        Assert.Null(LiveLayouts.Find(null));
    }
}

public class LiveViewSelectionTests
{
    private static readonly int[] Cameras = [10, 20, 30, 40];

    [Fact]
    public void Parse_reads_layout_and_ids_and_round_trips()
    {
        var selection = LiveViewSelection.Parse(" 3-row ", "10, 20,x,30");
        Assert.Equal("3-row", selection.LayoutKey);
        Assert.Equal([10, 20, 0, 30], selection.CameraIds);
        Assert.Equal("layout=3-row&cams=10,20,0,30", selection.ToQuery());
        Assert.Equal(selection.CameraIds, LiveViewSelection.Parse("3-row", "10,20,0,30").CameraIds);
    }

    [Fact]
    public void Resolve_without_selection_shows_all_cameras_up_to_the_maximum()
    {
        var resolved = LiveViewSelection.Empty.Resolve(Cameras);
        Assert.Equal(4, resolved.Layout.CameraCount);
        Assert.Equal(Cameras, resolved.SlotCameraIds);

        var many = LiveViewSelection.Empty.Resolve(Enumerable.Range(1, 10).ToList());
        Assert.Equal(LiveLayouts.MaxCameras, many.Layout.CameraCount);
        Assert.Equal(Enumerable.Range(1, 6), many.SlotCameraIds);

        var none = LiveViewSelection.Empty.Resolve([]);
        Assert.Equal(1, none.Layout.CameraCount);
        Assert.Equal([0], none.SlotCameraIds);
    }

    [Fact]
    public void Resolve_drops_unknown_and_duplicate_cameras_and_fills_gaps()
    {
        var resolved = new LiveViewSelection("3-main-left", [99, 20, 20]).Resolve(Cameras);
        Assert.Equal("3-main-left", resolved.Layout.Key);
        Assert.Equal([10, 20, 30], resolved.SlotCameraIds);
    }

    [Fact]
    public void Resolve_pads_and_trims_to_the_layout()
    {
        var padded = new LiveViewSelection("6-grid-3x2", [40]).Resolve(Cameras);
        Assert.Equal([40, 10, 20, 30, 0, 0], padded.SlotCameraIds);

        var trimmed = new LiveViewSelection("2-stacked", [10, 20, 30, 40]).Resolve(Cameras);
        Assert.Equal([10, 20], trimmed.SlotCameraIds);

        var unknownLayout = new LiveViewSelection("nope", [30, 40]).Resolve(Cameras);
        Assert.Equal(2, unknownLayout.Layout.CameraCount);
        Assert.Equal([30, 40], unknownLayout.SlotCameraIds);
    }

    [Fact]
    public void Editing_helpers_keep_assignments_and_swap_duplicates()
    {
        var resolved = new LiveViewSelection("4-grid", [10, 20, 30, 40]).Resolve(Cameras);

        var fewer = resolved.WithCount(2).Resolve(Cameras);
        Assert.Equal([10, 20], fewer.SlotCameraIds);

        var relaid = resolved.WithLayout("4-main-left").Resolve(Cameras);
        Assert.Equal("4-main-left", relaid.Layout.Key);
        Assert.Equal([10, 20, 30, 40], relaid.SlotCameraIds);

        var swapped = resolved.WithCamera(0, 30).Resolve(Cameras);
        Assert.Equal([30, 20, 10, 40], swapped.SlotCameraIds);

        var replaced = fewer.WithCamera(1, 40).Resolve(Cameras);
        Assert.Equal([10, 40], replaced.SlotCameraIds);
    }
}
