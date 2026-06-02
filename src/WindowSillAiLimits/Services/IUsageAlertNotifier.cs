using System.Runtime.InteropServices;

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

public sealed class NativeUsageAlertNotifier : IUsageAlertNotifier
{
    private const uint MbOk = 0x00000000;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbTopmost = 0x00040000;

    public void NotifyUsageAboveExpected(UsageAboveExpectedAlert alert)
    {
        var title = "AI Limits";
        var message = $"{alert.ProviderName} {alert.WindowLabel}: realizado {alert.UsedPercent:0}% passou o previsto {alert.ExpectedPercent:0}%.";
        _ = Task.Run(() => MessageBoxW(IntPtr.Zero, message, title, MbOk | MbIconWarning | MbTopmost));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
