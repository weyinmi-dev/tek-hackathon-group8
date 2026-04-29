using Application.Abstractions.Messaging;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.Conversations;

internal sealed class DeleteConversationCommandHandler(
    IConversationRepository conversations,
    IUnitOfWork uow) : ICommandHandler<DeleteConversationCommand>
{
    public async Task<Result> Handle(DeleteConversationCommand cmd, CancellationToken ct)
    {
        Conversation? conv = await conversations.GetAsync(cmd.ConversationId, ct);
        if (conv is null || conv.UserId != cmd.UserId)
        {
            return Result.Failure(Error.NotFound("Conversation.NotFound", "Conversation not found."));
        }

        conversations.Remove(conv);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
