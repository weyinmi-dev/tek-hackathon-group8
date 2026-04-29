using Application.Abstractions.Messaging;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.Conversations;

internal sealed class RenameConversationCommandHandler(
    IConversationRepository conversations,
    IUnitOfWork uow) : ICommandHandler<RenameConversationCommand>
{
    public async Task<Result> Handle(RenameConversationCommand cmd, CancellationToken ct)
    {
        Conversation? conv = await conversations.GetAsync(cmd.ConversationId, ct);
        if (conv is null || conv.UserId != cmd.UserId)
        {
            return Result.Failure(Error.NotFound("Conversation.NotFound", "Conversation not found."));
        }
        conv.Rename(cmd.NewTitle);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
