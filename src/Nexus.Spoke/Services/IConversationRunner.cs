namespace Nexus.Spoke.Services;

public interface IConversationRunner
{
    Task<string> InvokeAsync(string? ccSessionId, string message, string? workingDirectory = null, CancellationToken cancellationToken = default);
}
