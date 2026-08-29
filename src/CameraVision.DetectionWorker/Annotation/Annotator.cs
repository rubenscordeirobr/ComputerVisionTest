using CameraVision.Tracking;
using Compunet.YoloSharp.Data;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CameraVision.Annotation;

/// <summary>Draws bounding box, class label, confidence and tracking ID on frames.</summary>
public static class Annotator
{
    private static readonly Color[] Palette =
    [
        Color.ParseHex("FF3838"), Color.ParseHex("FF9D97"), Color.ParseHex("FF701F"),
        Color.ParseHex("FFB21D"), Color.ParseHex("CFD231"), Color.ParseHex("48F90A"),
        Color.ParseHex("92CC17"), Color.ParseHex("3DDB86"), Color.ParseHex("1A9334"),
        Color.ParseHex("00D4BB"), Color.ParseHex("2C99A8"), Color.ParseHex("00C2FF"),
        Color.ParseHex("344593"), Color.ParseHex("6473FF"), Color.ParseHex("0018EC"),
        Color.ParseHex("8438FF"), Color.ParseHex("520085"), Color.ParseHex("CB38FF"),
        Color.ParseHex("FF95C8"), Color.ParseHex("FF37C7"),
    ];

    private static readonly Font? Font = CreateFont();

    private static Font? CreateFont()
    {
        try
        {
            if (SystemFonts.TryGet("Arial", out var family) || SystemFonts.TryGet("Segoe UI", out family))
                return family.CreateFont(15, FontStyle.Bold);
            return SystemFonts.Families.FirstOrDefault().CreateFont(15, FontStyle.Bold);
        }
        catch
        {
            // No system fonts available: boxes are still drawn, labels are skipped.
            return null;
        }
    }

    public static void Draw(Image<Rgb24> image, List<(Detection Detection, TrackedObject Track)> items)
    {
        if (items.Count == 0)
            return;

        var thickness = Math.Max(2f, image.Width / 640f);

        image.Mutate(ctx =>
        {
            foreach (var (_, track) in items)
            {
                var color = Palette[track.ClassId % Palette.Length];
                var bounds = track.Bounds;
                ctx.Draw(color, thickness, new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height));

                if (Font == null)
                    continue;

                var label = $"{ClassLabels.Translate(track.ClassName)} {(int)(track.Confidence * 100)}% #{track.Id}";
                var textSize = TextMeasurer.MeasureSize(label, new TextOptions(Font));
                var labelHeight = textSize.Height + 6;
                var labelY = bounds.Y >= labelHeight ? bounds.Y - labelHeight : bounds.Y;

                ctx.Fill(color, new RectangleF(bounds.X, labelY, textSize.Width + 8, labelHeight));
                ctx.DrawText(label, Font, Color.White, new PointF(bounds.X + 4, labelY + 3));
            }
        });
    }
}
