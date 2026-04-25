using SharedKernel;

namespace Modules.Ai.Domain.Conversations;

public sealed class ChatLog : Entity
{
    private ChatLog(Guid id, Guid? userId, string actor, string question, string answer, string skillTrace, double confidence) : base(id)
    {
        UserId = userId;
        Actor = actor;
        Question = question;
        Answer = answer;
        SkillTrace = skillTrace;
        Confidence = confidence;
        OccurredAtUtc = DateTime.UtcNow;
    }

    private ChatLog() { }

    public Guid? UserId { get; private set; }
    public string Actor { get; private set; } = null!;
    public string Question { get; private set; } = null!;
    public string Answer { get; private set; } = null!;
    public string SkillTrace { get; private set; } = null!;
    public double Confidence { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static ChatLog Record(Guid? userId, string actor, string question, string answer, string skillTrace, double confidence) =>
        new(Guid.NewGuid(), userId, actor, question, answer, skillTrace, confidence);
}

public interface IChatLogRepository
{
    Task AddAsync(ChatLog log, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
