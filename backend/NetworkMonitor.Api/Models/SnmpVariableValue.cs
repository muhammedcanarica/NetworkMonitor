namespace NetworkMonitor.Api.Models;

public sealed record SnmpVariableValue(
    string Oid,
    string? Value,
    string Type,
    ulong? NumericValue = null);
