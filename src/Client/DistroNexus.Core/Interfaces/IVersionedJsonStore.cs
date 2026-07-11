using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IVersionedJsonStore<T>
{
    Task<StoreResult<VersionedDocument<T>>> ReadAsync(CancellationToken cancellationToken = default);
    Task<StoreResult<VersionedDocument<T>>> WriteAsync(T value, long expectedRevision, CancellationToken cancellationToken = default);
}
