using WindowSill.API;

namespace WindowSillAiLimits.Settings;

internal sealed class InMemorySettingsProvider : ISettingsProvider
{
    private readonly Dictionary<string, object?> _values = [];

    public event Windows.Foundation.TypedEventHandler<ISettingsProvider, SettingChangedEventArgs>? SettingChanged;

    public bool IsActivelyControlledByAdmin<T>(SettingDefinition<T> settingDefinition)
        => false;

    public T GetSetting<T>(SettingDefinition<T> settingDefinition)
        => _values.TryGetValue(settingDefinition.Name, out var value) && value is T typedValue
            ? typedValue
            : settingDefinition.DefaultValue;

    public void SetSetting<T>(SettingDefinition<T> settingDefinition, T value)
    {
        _values[settingDefinition.Name] = value;
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(settingDefinition.Name, value));
    }

    public void ResetSetting<T>(SettingDefinition<T> settingDefinition)
    {
        _values.Remove(settingDefinition.Name);
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(settingDefinition.Name, settingDefinition.DefaultValue));
    }

    public void OpenSettingsPageForSill(string internalSillName, string? sillSettingViewTitle)
    {
    }
}
