using SharedKernel;

namespace Modules.Ai.Domain.Conversations;

/// <summary>
/// One turn in a <see cref="Conversation"/>. Stored append-only — never edited
/// or deleted in isolation; deleting the parent conversation cascades. <c>Metadata</c>
/// holds JSON for assistant-side artifacts (skill trace, provider, confidence,
/// attachments) so the UI can rehydrate the full answer card on session restore.
/// </summary>
public sealed class Message : Entity
{
    private Message(
        Guid id,
        Guid conversationId,
        MessageRole role,
        string content,
        string? metadata,
        int? promptTokens,
        int? completionTokens) : base(id)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Metadata = metadata;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        CreatedAtUtc = DateTime.UtcNow;
    }

    private Message() { }

    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = null!;

    /// <summary>JSON blob — see <c>MessageMetadata</c> in the Application layer for the shape.</summary>
    public string? Metadata { get; private set; }

    public int? PromptTokens { get; private set; }
    public int? CompletionTokens { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    internal static Message Create(
        Guid conversationId,
        MessageRole role,
        string content,
        string? metadata = null,
        int? promptTokens = null,
        int? completionTokens = null) =>
        new(Guid.NewGuid(), conversationId, role, content, metadata, promptTokens, completionTokens);
}
