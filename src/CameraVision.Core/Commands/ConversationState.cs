namespace CameraVision.Core.Commands;

/// <summary>
/// What the agent's last reply to the sender is still waiting for (within the follow-up
/// window): the validity after "até quando?" (SPEC-17), or a yes/no to the supported
/// command it offered in place of an unsupported request (SPEC-20).
/// </summary>
public sealed record ConversationState(bool ExpectingDuration = false, CommandInterpretation? PendingOffer = null)
{
    public static readonly ConversationState None = new();
}
