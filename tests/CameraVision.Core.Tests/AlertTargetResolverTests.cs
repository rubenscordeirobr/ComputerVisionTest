using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;

namespace CameraVision.Core.Tests;

public class AlertTargetResolverTests
{
    // Tuesday 2026-09-08 15:00 — an ordinary afternoon.
    private static readonly DateTime Afternoon = new(2026, 9, 8, 15, 0, 0);

    private static Contact Contact(int id, string? email = null, string? phone = null) => new()
    {
        Id = id,
        TenantId = 1,
        Name = $"Contato {id}",
        Email = email,
        WhatsAppNumber = phone,
    };

    private static CaptureRule Rule(int id, int window, params AlertTrigger[] triggers) => new()
    {
        Id = id,
        TenantId = 1,
        Name = $"Regra {id}",
        Classes = ["person"],
        GroupWindowMinutes = window,
        Triggers = [.. triggers],
    };

    private static AlertTrigger Trigger(AlertChannel channel, params int[] contactIds) => new()
    {
        Channel = channel,
        ContactIds = [.. contactIds],
    };

    private static Capture Capture(string objectClass = "person", DateTime? startedAt = null) => new()
    {
        Id = 1,
        TenantId = 1,
        ObjectClass = objectClass,
        StartedAt = startedAt ?? Afternoon,
        EndedAt = (startedAt ?? Afternoon).AddSeconds(20),
    };

    private static IReadOnlyDictionary<int, Contact> Contacts(params Contact[] contacts) =>
        contacts.ToDictionary(c => c.Id);

    [Fact]
    public void Resolves_one_target_per_contact_and_channel()
    {
        var rule = Rule(1, 3,
            Trigger(AlertChannel.Email, 1, 2),
            Trigger(AlertChannel.WhatsApp, 1));
        var contacts = Contacts(
            Contact(1, "a@x.com", "+5549999990001"),
            Contact(2, "b@x.com"));

        var targets = AlertTargetResolver.Resolve(Capture(), [rule], contacts);

        Assert.Equal(3, targets.Count);
        Assert.Contains(targets, t => t is { Channel: AlertChannel.Email, Recipient: "a@x.com" });
        Assert.Contains(targets, t => t is { Channel: AlertChannel.Email, Recipient: "b@x.com" });
        Assert.Contains(targets, t => t is { Channel: AlertChannel.WhatsApp, Recipient: "+5549999990001" });
        Assert.All(targets, t => Assert.Same(rule, t.Rule));
    }

    [Fact]
    public void Class_mismatch_yields_nothing()
    {
        var rule = Rule(1, 3, Trigger(AlertChannel.Email, 1));
        var targets = AlertTargetResolver.Resolve(Capture("cat"), [rule], Contacts(Contact(1, "a@x.com")));
        Assert.Empty(targets);
    }

    [Fact]
    public void Class_matching_ignores_case()
    {
        var rule = Rule(1, 3, Trigger(AlertChannel.Email, 1));
        var targets = AlertTargetResolver.Resolve(Capture("Person"), [rule], Contacts(Contact(1, "a@x.com")));
        Assert.Single(targets);
    }

    [Fact]
    public void Disabled_rule_or_trigger_yields_nothing()
    {
        var disabledRule = Rule(1, 3, Trigger(AlertChannel.Email, 1));
        disabledRule.Enabled = false;
        var disabledTrigger = Trigger(AlertChannel.Email, 1);
        disabledTrigger.Enabled = false;
        var ruleWithDisabledTrigger = Rule(2, 3, disabledTrigger);
        var contacts = Contacts(Contact(1, "a@x.com"));

        Assert.Empty(AlertTargetResolver.Resolve(Capture(), [disabledRule, ruleWithDisabledTrigger], contacts));
    }

    [Fact]
    public void Rule_time_window_gates_by_capture_start()
    {
        var rule = Rule(1, 3, Trigger(AlertChannel.Email, 1));
        rule.ActiveFrom = new TimeOnly(22, 0);
        rule.ActiveTo = new TimeOnly(6, 0);
        var contacts = Contacts(Contact(1, "a@x.com"));

        Assert.Empty(AlertTargetResolver.Resolve(Capture(startedAt: Afternoon), [rule], contacts));
        Assert.Single(AlertTargetResolver.Resolve(Capture(startedAt: Afternoon.Date.AddHours(23)), [rule], contacts));
    }

    [Fact]
    public void Trigger_schedule_is_evaluated_at_capture_start()
    {
        var night = Trigger(AlertChannel.WhatsApp, 1);
        night.Kind = AlertTriggerKind.Weekly;
        night.Days = DaysOfWeek.Weekdays;
        night.StartTime = new TimeOnly(0, 0);
        night.EndTime = new TimeOnly(6, 0);
        var rule = Rule(1, 3, night);
        var contacts = Contacts(Contact(1, phone: "+5549999990001"));

        Assert.Single(AlertTargetResolver.Resolve(Capture(startedAt: Afternoon.Date.AddHours(3)), [rule], contacts));
        Assert.Empty(AlertTargetResolver.Resolve(Capture(startedAt: Afternoon), [rule], contacts));
    }

    [Fact]
    public void Same_contact_in_two_triggers_yields_one_target()
    {
        var rule = Rule(1, 3,
            Trigger(AlertChannel.WhatsApp, 1),
            Trigger(AlertChannel.WhatsApp, 1));
        var targets = AlertTargetResolver.Resolve(Capture(), [rule], Contacts(Contact(1, phone: "+5549999990001")));
        Assert.Single(targets);
    }

    [Fact]
    public void Two_contacts_sharing_a_number_yield_one_target()
    {
        var rule = Rule(1, 3, Trigger(AlertChannel.WhatsApp, 1, 2));
        var contacts = Contacts(
            Contact(1, phone: "+55 49 99999-0001"),
            Contact(2, phone: "5549999990001"));

        var targets = AlertTargetResolver.Resolve(Capture(), [rule], contacts);

        var target = Assert.Single(targets);
        Assert.Equal("+5549999990001", target.Recipient);
        Assert.Equal(1, target.Contact.Id);
    }

    [Fact]
    public void Temporary_notice_on_top_of_a_weekly_trigger_yields_one_message()
    {
        var weekly = Trigger(AlertChannel.WhatsApp, 1);
        weekly.Kind = AlertTriggerKind.Weekly;
        weekly.Days = DaysOfWeek.All;
        var temporary = Trigger(AlertChannel.WhatsApp, 1);
        temporary.Kind = AlertTriggerKind.Temporary;
        temporary.ActiveFrom = Afternoon.AddHours(-1);
        var rule = Rule(1, 3, weekly, temporary);

        var targets = AlertTargetResolver.Resolve(Capture(), [rule], Contacts(Contact(1, phone: "+5549999990001")));

        Assert.Single(targets);
    }

    [Fact]
    public void Contact_without_an_address_for_the_channel_is_skipped()
    {
        var rule = Rule(1, 3, Trigger(AlertChannel.WhatsApp, 1, 2));
        var contacts = Contacts(
            Contact(1, email: "only@mail.com"),
            Contact(2, phone: "+5549999990002"));

        var target = Assert.Single(AlertTargetResolver.Resolve(Capture(), [rule], contacts));
        Assert.Equal(2, target.Contact.Id);
    }

    [Fact]
    public void Deleted_contact_id_is_skipped()
    {
        var rule = Rule(1, 3, Trigger(AlertChannel.Email, 99, 1));
        var target = Assert.Single(AlertTargetResolver.Resolve(Capture(), [rule], Contacts(Contact(1, "a@x.com"))));
        Assert.Equal(1, target.Contact.Id);
    }

    [Fact]
    public void Rule_with_the_shortest_window_claims_a_shared_recipient()
    {
        var slow = Rule(1, 30, Trigger(AlertChannel.Email, 1));
        var fast = Rule(2, 3, Trigger(AlertChannel.Email, 1));
        var contacts = Contacts(Contact(1, "a@x.com"));

        var target = Assert.Single(AlertTargetResolver.Resolve(Capture(), [slow, fast], contacts));

        Assert.Same(fast, target.Rule);
    }

    [Fact]
    public void Different_channels_of_the_same_contact_are_separate_targets()
    {
        var rule = Rule(1, 3,
            Trigger(AlertChannel.Email, 1),
            Trigger(AlertChannel.WhatsApp, 1));
        var targets = AlertTargetResolver.Resolve(Capture(), [rule],
            Contacts(Contact(1, "a@x.com", "+5549999990001")));
        Assert.Equal(2, targets.Count);
    }
}
