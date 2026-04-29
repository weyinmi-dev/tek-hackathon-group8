namespace Modules.Ai.Domain.Conversations;

public interface IConversationRepository
{
    Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    void Remove(Conversation conversation);

    /// <summary>
    /// Stops tracking the entity so it won't participate in the next SaveChanges. Used by
    /// the AskCopilot path to bypass a phantom DbUpdateConcurrencyException — see
    /// AskCopilotCommandHandler for context.
    /// </summary>
    void Detach(Conversation conversation);

    /// <summary>
    /// Updates the activity scalars (<c>MessageCount</c>, <c>UpdatedAtUtc</c>,
    /// <c>LastMessageAtUtc</c>) for a conversation via a direct SQL UPDATE that bypasses
    /// the change tracker. Returns the number of rows affected.
    /// </summary>
    Task<int> UpdateActivityAsync(Guid id, int messageCount, DateTime updatedAtUtc, DateTime lastMessageAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Lists a user's conversations, newest first. Includes message-count + last-activity for the sidebar.</summary>
    Task<IReadOnlyList<Conversation>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
