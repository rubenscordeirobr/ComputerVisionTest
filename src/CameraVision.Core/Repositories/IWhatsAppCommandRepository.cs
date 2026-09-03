using CameraVision.Core.Entities;

namespace CameraVision.Core.Repositories;

public interface IWhatsAppCommandRepository
{
    /// <summary>Inserts the row; false when a row with the same MessageId already exists.</summary>
    Task<bool> TryAddAsync(WhatsAppCommandLog log, CancellationToken ct = default);

    /// <summary>Oldest pending rows first.</summary>
    Task<IReadOnlyList<WhatsAppCommandLog>> GetPendingAsync(int take, CancellationToken ct = default);

    Task UpdateAsync(WhatsAppCommandLog log, CancellationToken ct = default);

    /// <summary>The sender's most recent processed (non-pending) row — the conversation state.</summary>
    Task<WhatsAppCommandLog?> GetLastProcessedBySenderAsync(string senderNumber, CancellationToken ct = default);

    /// <summary>Messages received from the sender since <paramref name="since"/> (antiflood).</summary>
    Task<int> CountBySenderSinceAsync(string senderNumber, DateTime since, CancellationToken ct = default);

    /// <summary>Newest rows first, for the system page.</summary>
    Task<IReadOnlyList<WhatsAppCommandLog>> GetRecentAsync(int take, CancellationToken ct = default);
}
