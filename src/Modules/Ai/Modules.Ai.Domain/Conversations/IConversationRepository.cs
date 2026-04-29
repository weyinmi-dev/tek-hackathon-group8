namespace Modules.Ai.Domain.Conversations;

public interface IConversationRepository
{
    Task<Conversation?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Conversation?> GetWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    void Remove(Conversation conversation);

    /// <summary>Lists a user's conversations, newest first. Includes message-count + last-activity for the sidebar.</summary>
    Task<IReadOnlyList<Conversation>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
