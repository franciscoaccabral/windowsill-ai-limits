using System.Globalization;
using System.Text;

using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace WindowSillAiLimits.Services;

public interface IUsageAlertNotifier
{
    void NotifyUsageAboveExpected(UsageAboveExpectedAlert alert);
}

public sealed record UsageAboveExpectedAlert(
    string ProviderName,
    string WindowLabel,
    double UsedPercent,
    double ExpectedPercent);

public sealed record UsageAlertNotification(
    string Title,
    string Body,
    string Group,
    string Tag,
    DateTimeOffset ExpiresAt,
    bool ExpiresOnReboot);

public interface IUsageAlertNotificationSender
{
    void Show(UsageAlertNotification notification);
}

public sealed class NativeUsageAlertNotifier : IUsageAlertNotifier
{
    private const string NotificationTitle = "AI Limits";
    private const string NotificationGroup = "ai-limits";

    private readonly IUsageAlertNotificationSender _sender;
    private readonly Func<DateTimeOffset> _clock;

    public NativeUsageAlertNotifier()
        : this(new WindowsAppNotificationSender(), () => DateTimeOffset.Now)
    {
    }

    public NativeUsageAlertNotifier(IUsageAlertNotificationSender sender, Func<DateTimeOffset>? clock = null)
    {
        _sender = sender;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public void NotifyUsageAboveExpected(UsageAboveExpectedAlert alert)
    {
        try
        {
            _sender.Show(CreateNotification(alert, _clock()));
        }
        catch (Exception ex)
        {
            AiLimitsDiagnostics.Error("native usage alert notification failed", ex);
        }
    }

    public static UsageAlertNotification CreateNotification(UsageAboveExpectedAlert alert, DateTimeOffset now)
        => new(
            LocalizedText.Get("DisplayName"),
            LocalizedText.Format(
                "Notification.UsageAboveExpected.BodyFormat",
                alert.ProviderName,
                alert.WindowLabel,
                alert.UsedPercent.ToString("0", CultureInfo.InvariantCulture),
                alert.ExpectedPercent.ToString("0", CultureInfo.InvariantCulture)),
            NotificationGroup,
            $"{CreateTagPart(alert.ProviderName)}-{CreateTagPart(alert.WindowLabel)}",
            now.AddHours(6),
            ExpiresOnReboot: true);

    private static string CreateTagPart(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        if (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "alert" : builder.ToString();
    }
}

public sealed class WindowsAppNotificationSender : IUsageAlertNotificationSender
{
    public void Show(UsageAlertNotification notification)
    {
        if (!AppNotificationManager.IsSupported())
        {
            throw new InvalidOperationException("Windows app notifications are not supported for the current host.");
        }

        var appNotification = new AppNotificationBuilder()
            .AddText(notification.Title)
            .AddText(notification.Body)
            .SetGroup(notification.Group)
            .SetTag(notification.Tag)
            .BuildNotification();

        appNotification.Expiration = notification.ExpiresAt;
        appNotification.ExpiresOnReboot = notification.ExpiresOnReboot;

        AppNotificationManager.Default.Show(appNotification);
    }
}
