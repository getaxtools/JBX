namespace Jbx.Services.Probing;

public readonly record struct GateResult<T>(bool IsTimedOut, T? Value);
