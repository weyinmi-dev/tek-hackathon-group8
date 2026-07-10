using Application.Abstractions.Messaging;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.Conversations;

/// <summary>
/// Appends new turns to a conversation for the MAF chat-history provider's store step
/// (Phase 2 §8.1). Role strings are the MAF <c>ChatRole</c> names; anything unrecognised maps
/// to <see cref="MessageRole.User"/>. Blank content is skipped.
/// </summary>
public sealed record AppendMessagesCommand(Guid ConversationId, IReadOnlyList<ConversationTurn> Turns)
    : ICommand;

public sealed record ConversationTurn(string Role, string Content);

internal sealed class AppendMessagesCommandHandler(IConversationRepository conversations, IUnitOfWork uow)
    : ICommandHandler<AppendMessagesCommand>
{
    public async Task<Result> Handle(AppendMessagesCommand request, CancellationToken cancellationToken)
    {
        Conversation? conversation = await conversations.GetWithMessagesAsync(request.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result.Failure(Error.NotFound(
                "Conversation.NotFound", $"Conversation {request.ConversationId} was not found."));
        }

        foreach (ConversationTurn turn in request.Turns)
        {
            if (string.IsNullOrWhiteSpace(turn.Content))
            {
                continue;
            }
            MessageRole role = Enum.TryParse(turn.Role, ignoreCase: true, out MessageRole parsed)
                ? parsed
                : MessageRole.User;
            conversation.AppendMessage(role, turn.Content);
        }

        await uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
