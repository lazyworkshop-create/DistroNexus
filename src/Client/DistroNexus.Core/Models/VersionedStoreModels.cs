using System.Text.Json.Nodes;

namespace DistroNexus.Core.Models;

public sealed record VersionedDocument<T>(int SchemaVersion, long Revision, DateTimeOffset UpdatedAt, T Value, JsonObject? ExtensionData = null);

public enum StoreErrorKind { None, NotFound, RevisionConflict, NewerSchema, InvalidDocument, IoFailure }

public sealed record StoreResult<T>(T? Value, StoreErrorKind Error, string? Message = null)
{
    public bool Succeeded => Error == StoreErrorKind.None;
    public static StoreResult<T> Success(T value) => new(value, StoreErrorKind.None);
    public static StoreResult<T> Failure(StoreErrorKind error, string message) => new(default, error, message);
}
