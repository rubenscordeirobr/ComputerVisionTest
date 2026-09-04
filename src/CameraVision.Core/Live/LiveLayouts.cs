namespace CameraVision.Core.Live;

/// <summary>
/// Catalog of the live-view grid templates, grouped by how many cameras they
/// show (1 to <see cref="MaxCameras"/>). Names are user-facing (PT-BR); keys are
/// stable identifiers used in URLs and saved preferences.
/// </summary>
public static class LiveLayouts
{
    public const int MaxCameras = 6;

    private static readonly double[] One = [1];
    private static readonly double[] Two = [1, 1];
    private static readonly double[] Three = [1, 1, 1];
    private static readonly double[] Four = [1, 1, 1, 1];
    private static readonly double[] Six = [1, 1, 1, 1, 1, 1];

    public static IReadOnlyList<LiveLayout> All { get; } =
    [
        // 1 camera
        new("1-single", "Tela única", One, One, [S(1, 1)]),

        // 2 cameras
        new("2-side-by-side", "Lado a lado", Two, One, [S(1, 1), S(2, 1)]),
        new("2-stacked", "Uma sobre a outra", One, Two, [S(1, 1), S(1, 2)]),
        new("2-main-left", "Destaque à esquerda", [2, 1], One, [S(1, 1), S(2, 1)]),
        new("2-main-top", "Destaque acima", One, [2, 1], [S(1, 1), S(1, 2)]),

        // 3 cameras
        new("3-row", "Três em linha", Three, One, [S(1, 1), S(2, 1), S(3, 1)]),
        new("3-column", "Três em coluna", One, Three, [S(1, 1), S(1, 2), S(1, 3)]),
        new("3-top-wide", "Uma acima, duas abaixo", Two, [2, 1], [S(1, 1, 2, 1), S(1, 2), S(2, 2)]),
        new("3-bottom-wide", "Duas acima, uma abaixo", Two, [1, 2], [S(1, 1), S(2, 1), S(1, 2, 2, 1)]),
        new("3-main-left", "Uma à esquerda, duas à direita", [2, 1], Two, [S(1, 1, 1, 2), S(2, 1), S(2, 2)]),
        new("3-main-right", "Duas à esquerda, uma à direita", [1, 2], Two, [S(1, 1), S(1, 2), S(2, 1, 1, 2)]),

        // 4 cameras
        new("4-grid", "Grade 2×2", Two, Two, [S(1, 1), S(2, 1), S(1, 2), S(2, 2)]),
        new("4-row", "Quatro em linha", Four, One, [S(1, 1), S(2, 1), S(3, 1), S(4, 1)]),
        new("4-main-left", "Destaque à esquerda, três à direita", [3, 1], Three,
            [S(1, 1, 1, 3), S(2, 1), S(2, 2), S(2, 3)]),
        new("4-main-right", "Três à esquerda, destaque à direita", [1, 3], Three,
            [S(1, 1), S(1, 2), S(1, 3), S(2, 1, 1, 3)]),
        new("4-main-top", "Destaque acima, três abaixo", Three, [3, 1],
            [S(1, 1, 3, 1), S(1, 2), S(2, 2), S(3, 2)]),
        new("4-main-bottom", "Três acima, destaque abaixo", Three, [1, 3],
            [S(1, 1), S(2, 1), S(3, 1), S(1, 2, 3, 1)]),

        // 5 cameras
        new("5-main-left", "Destaque à esquerda, quatro à direita", [2, 1, 1], Two,
            [S(1, 1, 1, 2), S(2, 1), S(3, 1), S(2, 2), S(3, 2)]),
        new("5-main-right", "Quatro à esquerda, destaque à direita", [1, 1, 2], Two,
            [S(1, 1), S(2, 1), S(1, 2), S(2, 2), S(3, 1, 1, 2)]),
        new("5-main-top", "Destaque acima, quatro abaixo", Four, [2, 1],
            [S(1, 1, 4, 1), S(1, 2), S(2, 2), S(3, 2), S(4, 2)]),
        new("5-two-three", "Duas acima, três abaixo", Six, Two,
            [S(1, 1, 3, 1), S(4, 1, 3, 1), S(1, 2, 2, 1), S(3, 2, 2, 1), S(5, 2, 2, 1)]),
        new("5-three-two", "Três acima, duas abaixo", Six, Two,
            [S(1, 1, 2, 1), S(3, 1, 2, 1), S(5, 1, 2, 1), S(1, 2, 3, 1), S(4, 2, 3, 1)]),

        // 6 cameras
        new("6-grid-3x2", "Grade 3×2", Three, Two,
            [S(1, 1), S(2, 1), S(3, 1), S(1, 2), S(2, 2), S(3, 2)]),
        new("6-grid-2x3", "Grade 2×3", Two, Three,
            [S(1, 1), S(2, 1), S(1, 2), S(2, 2), S(1, 3), S(2, 3)]),
        new("6-main-corner", "Destaque no canto, cinco ao redor", Three, Three,
            [S(1, 1, 2, 2), S(3, 1), S(3, 2), S(1, 3), S(2, 3), S(3, 3)]),
        new("6-two-main-top", "Dois destaques acima, quatro abaixo", Four, [2, 1],
            [S(1, 1, 2, 1), S(3, 1, 2, 1), S(1, 2), S(2, 2), S(3, 2), S(4, 2)]),
        new("6-two-main-bottom", "Quatro acima, dois destaques abaixo", Four, [1, 2],
            [S(1, 1), S(2, 1), S(3, 1), S(4, 1), S(1, 2, 2, 1), S(3, 2, 2, 1)]),
    ];

    /// <summary>Templates showing exactly <paramref name="cameraCount"/> cameras.</summary>
    public static IReadOnlyList<LiveLayout> For(int cameraCount) =>
        All.Where(l => l.CameraCount == cameraCount).ToList();

    public static LiveLayout? Find(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : All.FirstOrDefault(l => l.Key == key.Trim());

    /// <summary>The first (simplest) template for a camera count, clamped to 1..<see cref="MaxCameras"/>.</summary>
    public static LiveLayout Default(int cameraCount) =>
        For(Math.Clamp(cameraCount, 1, MaxCameras))[0];

    private static LiveSlot S(int column, int row, int columnSpan = 1, int rowSpan = 1) =>
        new(column, row, columnSpan, rowSpan);
}
