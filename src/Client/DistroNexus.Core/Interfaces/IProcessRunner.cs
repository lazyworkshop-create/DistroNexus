using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
