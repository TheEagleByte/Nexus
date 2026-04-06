using Nexus.Spoke.Models;

namespace Nexus.Spoke.Services;

public interface IPromptAssembler
{
    Task<string> AssembleAsync(PromptAssemblyContext context, CancellationToken cancellationToken = default);
}
