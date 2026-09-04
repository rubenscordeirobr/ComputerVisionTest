using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface IAgentSuggestionRepository
{
    Task AddAsync(AgentSuggestion suggestion, CancellationToken ct = default);

    /// <summary>Newest first, for the system page.</summary>
    Task<IReadOnlyList<AgentSuggestion>> GetRecentAsync(int take, CancellationToken ct = default);

    /// <summary>Suggestions nobody has marked as seen yet.</summary>
    Task<int> CountNewAsync(CancellationToken ct = default);

    Task MarkReviewedAsync(int id, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
