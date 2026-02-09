using System.ComponentModel;

namespace DockMonitor.Service.Config;

public sealed class MonitorConfig
{
    public List<DockDeviceConfig> DockDevices { get; set; } = new();
    public List<string> RestartDevices { get; set; } = new();

    public ProfileConfig Docked { get; set; } = new();
    public ProfileConfig Undocked { get; set; } = new();

    public int PollIntervalMs { get; set; } = 1000;
}

[TypeConverter(typeof(DockDeviceConfigTypeConverter))]
public sealed class DockDeviceConfig
{
    public string Token { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public sealed class DockDeviceConfigTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string s)
        {
            return new DockDeviceConfig { Token = s };
        }

        return base.ConvertFrom(context, culture, value);
    }
}

public sealed class ProfileConfig
{
    public string? AudioProfile { get; set; }
    public bool ResetResolution { get; set; } = false;
    public int TempWidth { get; set; } = 1280;
    public int TempHeight { get; set; } = 1024;
    public int RestoreDelayMs { get; set; } = 2000;
    public List<string> BluetoothConnect { get; set; } = new();
}
