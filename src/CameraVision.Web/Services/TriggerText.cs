using CameraVision.Core.Entities;

namespace CameraVision.Web.Services;

/// <summary>PT-BR summaries of notification triggers (schedule, contacts, antiflood window).</summary>
public static class TriggerText
{
    /// <summary>Monday-first day labels, in display order.</summary>
    public static readonly IReadOnlyList<(DaysOfWeek Day, string Label)> DayLabels =
    [
        (DaysOfWeek.Monday, "Seg"),
        (DaysOfWeek.Tuesday, "Ter"),
        (DaysOfWeek.Wednesday, "Qua"),
        (DaysOfWeek.Thursday, "Qui"),
        (DaysOfWeek.Friday, "Sex"),
        (DaysOfWeek.Saturday, "Sáb"),
        (DaysOfWeek.Sunday, "Dom"),
    ];

    /// <summary>"Todos os dias", "Seg–Sex", "Sáb, Dom", "Seg–Qui, Sáb" (runs of 3+ days are compacted).</summary>
    public static string Days(DaysOfWeek days)
    {
        if (days == DaysOfWeek.All)
            return "Todos os dias";
        if (days == DaysOfWeek.None)
            return "Nenhum dia";

        var parts = new List<string>();
        var i = 0;
        while (i < DayLabels.Count)
        {
            if ((days & DayLabels[i].Day) == 0)
            {
                i++;
                continue;
            }
            var start = i;
            while (i + 1 < DayLabels.Count && (days & DayLabels[i + 1].Day) != 0)
                i++;
            if (i - start + 1 >= 3)
                parts.Add($"{DayLabels[start].Label}–{DayLabels[i].Label}");
            else
                for (var j = start; j <= i; j++)
                    parts.Add(DayLabels[j].Label);
            i++;
        }
        return string.Join(", ", parts);
    }

    /// <summary>"Sempre", "Seg–Sex 00:00–06:00", "Sáb, Dom · dia inteiro", "Temporário até 02/09 18:00"…</summary>
    public static string Schedule(AlertTrigger trigger, DateTime now)
    {
        switch (trigger.Kind)
        {
            case AlertTriggerKind.Temporary:
                if (trigger.IsExpiredAt(now))
                    return $"Temporário · expirado em {Stamp(trigger.ExpiresAt!.Value, now)}";
                return trigger.ExpiresAt is { } until
                    ? $"Temporário até {Stamp(until, now)}"
                    : "Temporário até desativar";
            case AlertTriggerKind.Weekly:
                var days = Days(trigger.Days);
                return trigger.IsAllDay
                    ? $"{days} · dia inteiro"
                    : $"{days} {trigger.StartTime:HH:mm}–{trigger.EndTime:HH:mm}";
            default:
                return "Sempre";
        }
    }

    /// <summary>"Rubens, Maria" — names of the trigger's contacts (deleted ones flagged).</summary>
    public static string Contacts(AlertTrigger trigger, IReadOnlyDictionary<int, Contact> contactsById, int max = 3)
    {
        if (trigger.ContactIds.Count == 0)
            return "Nenhum contato";
        var names = trigger.ContactIds
            .Select(id => contactsById.TryGetValue(id, out var contact) ? contact.Name : "(contato removido)")
            .ToList();
        return names.Count <= max
            ? string.Join(", ", names)
            : $"{string.Join(", ", names.Take(max))} +{names.Count - max}";
    }

    public static string Summary(AlertTrigger trigger, IReadOnlyDictionary<int, Contact> contactsById, DateTime now) =>
        $"{ChannelUi.Label(trigger.Channel)} · {Contacts(trigger, contactsById)} · {Schedule(trigger, now)}" +
        (trigger.Enabled ? "" : " (desativada)");

    /// <summary>"Imediato" / "3 min".</summary>
    public static string Window(int groupWindowMinutes) =>
        groupWindowMinutes <= 0 ? "Imediato" : $"{groupWindowMinutes} min";

    /// <summary>A temporary notice that is currently in force (drives the warning chips).</summary>
    public static bool IsRunningTemporary(AlertTrigger trigger, DateTime now) =>
        trigger.Kind == AlertTriggerKind.Temporary && trigger.Enabled && trigger.IsActiveAt(now);

    private static string Stamp(DateTime at, DateTime now) =>
        at.Year == now.Year ? at.ToString("dd/MM HH:mm") : at.ToString("dd/MM/yyyy HH:mm");
}
