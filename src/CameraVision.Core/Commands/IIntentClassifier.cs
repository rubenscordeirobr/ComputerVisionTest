using CameraVision.Core.Entities;

namespace CameraVision.Core.Commands;

/// <summary>
/// Interprets one WhatsApp message: keyword rules first, the configured LLM as a
/// fallback. Always answers — an unreadable message is <see cref="CommandIntent.Unknown"/>.
/// </summary>
public interface IIntentClassifier
{
    /// <param name="state">What the agent's last reply to this sender is waiting for ("até quando?", an offered command).</param>
    Task<CommandInterpretation> ClassifyAsync(string text, SystemSettings settings, DateTime now,
        ConversationState state, CancellationToken ct = default);
}
