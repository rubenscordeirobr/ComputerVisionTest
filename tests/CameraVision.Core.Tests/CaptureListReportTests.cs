using CameraVision.Core.Commands;
using CameraVision.Core.Entities;

namespace CameraVision.Core.Tests;

public class CaptureListReportTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 14, 0, 0);

    private static Capture Capture(int id, string camera = "Garagem", string objectClass = "person", int minutesAgo = 0) => new()
    {
        Id = id, CameraName = camera, ObjectClass = objectClass,
        StartedAt = Now.AddMinutes(-minutesAgo), EndedAt = Now.AddMinutes(-minutesAgo).AddSeconds(12),
    };

    private static string Link(Capture c) => $"https://cams.example/captures/{c.Id}/watch?token=t{c.Id}";

    [Fact]
    public void Lists_captures_with_number_camera_duration_and_link()
    {
        var text = CaptureListReport.Compose([Capture(41), Capture(40, "Portão", minutesAgo: 30)], total: 2, requested: 5,
            objectClass: "person", Link);

        var lines = text.Split('\n');
        Assert.Equal("Últimas 2 capturas de pessoa — CameraVision", lines[0]);
        Assert.Equal("1. 07/09 14:00 · Garagem · 00:12 · https://cams.example/captures/41/watch?token=t41", lines[1]);
        Assert.Equal("2. 07/09 13:30 · Portão · 00:12 · https://cams.example/captures/40/watch?token=t40", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void Any_class_lists_the_object_per_line()
    {
        var text = CaptureListReport.Compose([Capture(1, objectClass: "cat")], total: 1, requested: 5, objectClass: null, Link);
        Assert.StartsWith("Últimas 1 capturas — CameraVision", text);
        Assert.Contains("· Garagem · gato ·", text);
    }

    [Fact]
    public void Says_how_many_more_exist_and_how_to_ask()
    {
        var text = CaptureListReport.Compose([Capture(1)], total: 404, requested: 1, objectClass: "person", Link);
        Assert.Contains("Mostrando 1 de 404. Envie \"últimas 10 capturas de pessoa\" para ver mais.", text);
    }

    [Fact]
    public void Capped_request_is_explained()
    {
        var items = Enumerable.Range(1, 10).Select(i => Capture(i)).ToList();
        var text = CaptureListReport.Compose(items, total: 50, requested: 50, objectClass: "person", Link);
        Assert.Contains("Mostrando 10 de 50.", text);
        Assert.DoesNotContain("para ver mais", text);
        Assert.Contains("(O máximo por mensagem é 10.)", text);
    }

    [Fact]
    public void Empty_list() =>
        Assert.Equal("Nenhuma captura de gato encontrada.",
            CaptureListReport.Compose([], total: 0, requested: 5, objectClass: "cat", Link));

    [Fact]
    public void Unknown_class_reply_names_the_word() =>
        Assert.StartsWith("Não reconheci \"dinossauros\"", CaptureListReport.UnknownClass("dinossauros"));
}
