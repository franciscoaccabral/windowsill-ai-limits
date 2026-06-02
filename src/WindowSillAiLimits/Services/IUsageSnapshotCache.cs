using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services;

public interface IUsageSnapshotCache
{
    UsageSnapshot? Read();

    void Write(UsageSnapshot snapshot);
}
