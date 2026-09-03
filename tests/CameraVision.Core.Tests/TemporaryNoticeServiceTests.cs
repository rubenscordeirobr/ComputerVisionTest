using CameraVision.Core.Alerts;
using CameraVision.Core.Entities;
using CameraVision.Core.Repositories;

namespace CameraVision.Core.Tests;

public class TemporaryNoticeServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 14, 0, 0);
    private const int Tenant = 1;
    private const int Me = 10;
    private const int Other = 11;

    private static (TemporaryNoticeService Service, FakeRuleRepository Repo) Setup(params CaptureRule[] rules)
    {
        var repo = new FakeRuleRepository(rules);
        return (new TemporaryNoticeService(repo), repo);
    }

    private static CaptureRule Rule(int id, bool enabled = true, params AlertTrigger[] triggers) => new()
    {
        Id = id, TenantId = Tenant, Name = $"rule {id}", Enabled = enabled, Triggers = triggers.ToList(),
    };

    private static AlertTrigger Temporary(int id, params int[] contacts) => new()
    {
        Id = id, Kind = AlertTriggerKind.Temporary, Channel = AlertChannel.WhatsApp,
        ContactIds = contacts.ToList(), ActiveFrom = Now.AddHours(-1), ExpiresAt = Now.AddHours(1),
    };

    [Fact]
    public async Task Activate_creates_one_notice_per_enabled_rule()
    {
        var (service, repo) = Setup(Rule(1), Rule(2), Rule(3, enabled: false));

        var result = await service.ActivateForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now, Now.AddHours(8));

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Extended);
        Assert.Equal(Now.AddHours(8), result.ExpiresAt);
        Assert.All(repo.Saved, t =>
        {
            Assert.Equal(AlertTriggerKind.Temporary, t.Kind);
            Assert.Equal(AlertChannel.WhatsApp, t.Channel);
            Assert.Equal([Me], t.ContactIds);
            Assert.Equal(Now, t.ActiveFrom);
            Assert.Equal(DaysOfWeek.All, t.Days);
        });
        Assert.Equal([1, 2], repo.Saved.Select(t => t.CaptureRuleId));
    }

    [Fact]
    public async Task Activate_again_extends_the_own_notice_instead_of_duplicating()
    {
        var (service, repo) = Setup(Rule(1, true, Temporary(100, Me)));

        var result = await service.ActivateForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now, Now.AddHours(8));

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Extended);
        var saved = Assert.Single(repo.Saved);
        Assert.Equal(100, saved.Id);
        Assert.Equal(Now.AddHours(8), saved.ExpiresAt);
    }

    [Fact]
    public async Task Duration_is_clamped_to_the_limits()
    {
        var (service, _) = Setup(Rule(1));

        var tooLong = await service.ActivateForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now, Now.AddDays(10));
        Assert.Equal(Now + TemporaryNoticeService.MaxDuration, tooLong.ExpiresAt);

        var tooShort = await service.ActivateForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now, Now.AddMinutes(5));
        Assert.Equal(Now + TemporaryNoticeService.MinDuration, tooShort.ExpiresAt);

        var open = await service.ActivateForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now, null);
        Assert.Null(open.ExpiresAt);
    }

    [Fact]
    public async Task Set_expiry_moves_the_end_of_own_notices_only()
    {
        var (service, repo) = Setup(Rule(1, true, Temporary(100, Me), Temporary(101, Me, Other)));

        var updated = await service.SetExpiryForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now, Now.AddHours(3));

        Assert.Equal(1, updated);
        var saved = Assert.Single(repo.Saved);
        Assert.Equal(100, saved.Id);
        Assert.Equal(Now.AddHours(3), saved.ExpiresAt);
    }

    [Fact]
    public async Task End_deletes_own_notice_and_only_leaves_shared_ones()
    {
        var (service, repo) = Setup(
            Rule(1, true, Temporary(100, Me)),
            Rule(2, true, Temporary(101, Me, Other)),
            Rule(3, true, Temporary(102, Other)));

        var ended = await service.EndForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now);

        Assert.Equal(2, ended);
        Assert.Equal([100], repo.Deleted);
        var saved = Assert.Single(repo.Saved);
        Assert.Equal(101, saved.Id);
        Assert.Equal([Other], saved.ContactIds);
    }

    [Fact]
    public async Task End_ignores_expired_notices()
    {
        var expired = Temporary(100, Me);
        expired.ExpiresAt = Now.AddMinutes(-1);
        var (service, repo) = Setup(Rule(1, true, expired));

        Assert.Equal(0, await service.EndForContactAsync(Tenant, Me, AlertChannel.WhatsApp, Now));
        Assert.Empty(repo.Deleted);
    }

    [Fact]
    public async Task End_all_deletes_every_running_notice()
    {
        var expired = Temporary(102, Other);
        expired.ExpiresAt = Now.AddMinutes(-1);
        var rules = new[] { Rule(1, true, Temporary(100, Me)), Rule(2, true, Temporary(101, Other), expired) };
        var (service, repo) = Setup(rules);

        Assert.Equal(2, await service.EndAllRunningAsync(rules, Now));
        Assert.Equal([100, 101], repo.Deleted);
    }

    /// <summary>In-memory stand-in: records trigger writes, serves the rules it was built with.</summary>
    private sealed class FakeRuleRepository(IEnumerable<CaptureRule> rules) : ICaptureRuleRepository
    {
        private readonly List<CaptureRule> _rules = rules.ToList();

        public List<AlertTrigger> Saved { get; } = [];
        public List<int> Deleted { get; } = [];

        public Task<IReadOnlyList<CaptureRule>> GetAllAsync(int? tenantId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CaptureRule>>(_rules.Where(r => tenantId == null || r.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<CaptureRule>> GetEnabledAsync(int? tenantId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CaptureRule>>(_rules.Where(r => r.Enabled && (tenantId == null || r.TenantId == tenantId)).ToList());

        public Task<CaptureRule?> GetByIdAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));

        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(_rules.Count > 0);
        public Task AddAsync(CaptureRule rule, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(CaptureRule rule, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task ReplaceTriggersAsync(int ruleId, IReadOnlyList<AlertTrigger> triggers, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task SaveTriggerAsync(AlertTrigger trigger, CancellationToken ct = default)
        {
            Saved.Add(trigger);
            return Task.CompletedTask;
        }

        public Task DeleteTriggersAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            Deleted.AddRange(ids);
            return Task.CompletedTask;
        }
    }
}
