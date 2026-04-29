using SharedKernel;

namespace Modules.Ai.Domain.Conversations;

/// <summary>
/// A persistent chat session belonging to a single user. Holds an ordered series of
/// <see cref="Message"/> turns; the title is auto-derived from the first user prompt
/// (and overridable). All messages cascade-delete with the conversation.
/// </summary>
public sealed class Conversation : Entity
{
    private readonly List<Message> _messages = [];

    private Conversation(Guid id, Guid userId, string actorHandle, string title) : base(id)
    {
        UserId = userId;
        ActorHandle = actorHandle;
        Title = title;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    private Conversation() { }

    public Guid UserId { get; private set; }

    /// <summary>Cached author handle — convenience for listings without joining the Identity module.</summary>
    public string ActorHandle { get; private set; } = null!;

    public string Title { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? LastMessageAtUtc { get; private set; }
    public int MessageCount { get; private set; }

    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    public static Conversation Start(Guid userId, string actorHandle, string? initialTitle = null)
    {
        string title = string.IsNullOrWhiteSpace(initialTitle)
            ? "New conversation"
            : Truncate(initialTitle.Trim(), 80);
        return new Conversation(Guid.NewGuid(), userId, actorHandle, title);
    }

    public Message AppendMessage(
        MessageRole role,
        string content,
        string? metadata = null,
        int? promptTokens = null,
        int? completionTokens = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        var msg = Message.Create(Id, role, content, metadata, promptTokens, completionTokens);
        _messages.Add(msg);
        MessageCount++;
        LastMessageAtUtc = msg.CreatedAtUtc;
        UpdatedAtUtc = msg.CreatedAtUtc;

        // Auto-title from the first user prompt — keeps the sidebar useful without a
        // separate naming round-trip. Operators can rename later via Rename().
        if (role == MessageRole.User && string.Equals(Title, "New conversation", StringComparison.Ordinal))
        {
            Title = Truncate(content, 80);
        }
        return msg;
    }

    public void Rename(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return;
        Title = Truncate(newTitle.Trim(), 80);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
