using CameraVision.Core.Entities;

namespace CameraVision.Core.Alerts;

/// <summary>A resolved recipient of one capture: which rule, trigger and contact produced it.</summary>
public sealed record AlertTarget(
    CaptureRule Rule,
    AlertTrigger Trigger,
    AlertChannel Channel,
    Contact Contact,
    string Recipient);

/// <summary>
/// Pure rule evaluation: who gets notified about a capture, on which channel, attributed
/// to which rule. Schedules are evaluated at the capture's StartedAt ("who was on duty
/// when it happened"). Each (channel, normalized recipient) pair is claimed once across
/// every trigger and rule, so overlapping triggers — a temporary notice on top of a weekly
/// one, or two rules sharing a class — yield a single notification. The rule with the
/// shortest grouping window claims first.
/// </summary>
public static class AlertTargetResolver
{
    /// <summary>Enabled rules matching the capture's class and time window, fastest window first.</summary>
    public static IEnumerable<CaptureRule> MatchingRules(Capture capture, IEnumerable<CaptureRule> rules)
    {
        var timeOfDay = TimeOnly.FromDateTime(capture.StartedAt);
        return rules
            .Where(r => r.Enabled &&
                        r.Classes.Contains(capture.ObjectClass, StringComparer.OrdinalIgnoreCase) &&
                        r.IsActiveAt(timeOfDay))
            .OrderBy(r => r.GroupWindowMinutes)
            .ThenBy(r => r.Id);
    }

    public static IReadOnlyList<AlertTarget> Resolve(
        Capture capture,
        IReadOnlyList<CaptureRule> rules,
        IReadOnlyDictionary<int, Contact> contacts)
    {
        var moment = capture.StartedAt;
        var claimed = new HashSet<(AlertChannel Channel, string Recipient)>();
        var targets = new List<AlertTarget>();

        foreach (var rule in MatchingRules(capture, rules))
        {
            foreach (var trigger in rule.Triggers.Where(t => t.IsActiveAt(moment)).OrderBy(t => t.Id))
            {
                foreach (var contactId in trigger.ContactIds.Distinct())
                {
                    if (!contacts.TryGetValue(contactId, out var contact))
                        continue; // deleted contact
                    var recipient = RecipientNormalizer.Normalize(trigger.Channel, contact.AddressFor(trigger.Channel));
                    if (recipient == null)
                        continue; // no address for this channel
                    if (claimed.Add((trigger.Channel, recipient)))
                        targets.Add(new AlertTarget(rule, trigger, trigger.Channel, contact, recipient));
                }
            }
        }

        return targets;
    }
}
