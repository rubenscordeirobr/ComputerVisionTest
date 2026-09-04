namespace CameraVision.Core.Live;

/// <summary>
/// What the user asked to see: a layout key and the camera ids per slot, as
/// carried by the <c>/live?layout=…&amp;cams=…</c> query string and the saved
/// preference. Either part may be missing or stale (deleted camera, unknown
/// key); <see cref="Resolve"/> turns it into something displayable.
/// </summary>
public sealed record LiveViewSelection(string? LayoutKey, IReadOnlyList<int> CameraIds)
{
    public const int EmptySlot = 0;

    public static LiveViewSelection Empty { get; } = new(null, []);

    /// <summary>Parses the query values; malformed ids become empty slots.</summary>
    public static LiveViewSelection Parse(string? layout, string? cams)
    {
        var ids = (cams ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) && id > 0 ? id : EmptySlot)
            .ToList();
        return new LiveViewSelection(string.IsNullOrWhiteSpace(layout) ? null : layout.Trim(), ids);
    }

    /// <summary>Query string (without the leading "?") that reproduces this selection.</summary>
    public string ToQuery() =>
        $"layout={Uri.EscapeDataString(LayoutKey ?? "")}&cams={string.Join(',', CameraIds)}";

    /// <summary>
    /// Fits the selection to the cameras that actually exist: unknown or
    /// duplicate ids become empty slots, the layout falls back to the default
    /// for the slot count, slots are trimmed/padded to the layout, and empty
    /// slots are filled with the cameras not shown yet (in the given order).
    /// </summary>
    public LiveViewResolved Resolve(IReadOnlyList<int> availableCameraIds)
    {
        var slots = new List<int>();
        foreach (var id in CameraIds)
            slots.Add(availableCameraIds.Contains(id) && !slots.Contains(id) ? id : EmptySlot);

        var layout = LiveLayouts.Find(LayoutKey)
            ?? LiveLayouts.Default(slots.Count > 0 ? slots.Count : Math.Max(1, availableCameraIds.Count));

        if (slots.Count > layout.CameraCount)
            slots.RemoveRange(layout.CameraCount, slots.Count - layout.CameraCount);
        while (slots.Count < layout.CameraCount)
            slots.Add(EmptySlot);

        var unused = new Queue<int>(availableCameraIds.Where(id => !slots.Contains(id)));
        for (var i = 0; i < slots.Count && unused.Count > 0; i++)
            if (slots[i] == EmptySlot)
                slots[i] = unused.Dequeue();

        return new LiveViewResolved(layout, slots);
    }
}

/// <summary>A selection fitted to a layout: one camera id (or 0) per slot.</summary>
public sealed record LiveViewResolved(LiveLayout Layout, IReadOnlyList<int> SlotCameraIds)
{
    public LiveViewSelection ToSelection() => new(Layout.Key, SlotCameraIds);

    /// <summary>Changes how many cameras are shown, keeping the current assignments.</summary>
    public LiveViewSelection WithCount(int count) =>
        new(LiveLayouts.Default(count).Key, SlotCameraIds);

    public LiveViewSelection WithLayout(string layoutKey) => new(layoutKey, SlotCameraIds);

    /// <summary>
    /// Puts a camera in a slot. When that camera is already shown elsewhere the
    /// two slots swap, so a camera never appears twice.
    /// </summary>
    public LiveViewSelection WithCamera(int slotIndex, int cameraId)
    {
        var slots = SlotCameraIds.ToList();
        var other = slots.IndexOf(cameraId);
        if (other >= 0)
            slots[other] = slots[slotIndex];
        slots[slotIndex] = cameraId;
        return new LiveViewSelection(Layout.Key, slots);
    }
}
