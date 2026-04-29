namespace Modules.Ai.Domain.Conversations;

/// <summary>
/// Authoring role of a single message in a conversation. Mirrors the OpenAI chat
/// completion roles plus a tool slot for future tool-result echoes. Stored as int
/// so adding a value (e.g. <c>Function</c>) is non-breaking.
/// </summary>
public enum MessageRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3,
}
