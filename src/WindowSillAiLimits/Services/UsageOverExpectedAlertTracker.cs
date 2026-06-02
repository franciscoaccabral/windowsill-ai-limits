namespace WindowSillAiLimits.Services;

public sealed class UsageOverExpectedAlertTracker(IUsageAlertNotifier notifier)
{
    private readonly HashSet<string> _activeAlerts = [];

    public void Process(IReadOnlyList<UsageAboveExpectedAlert> alerts, bool isEnabled)
    {
        if (!isEnabled)
        {
            _activeAlerts.Clear();
            return;
        }

        var currentKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alert in alerts)
        {
            var key = $"{alert.ProviderName}:{alert.WindowLabel}";
            currentKeys.Add(key);
            if (_activeAlerts.Add(key))
            {
                notifier.NotifyUsageAboveExpected(alert);
            }
        }

        _activeAlerts.IntersectWith(currentKeys);
    }
}
