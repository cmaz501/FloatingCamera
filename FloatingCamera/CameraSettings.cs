using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FloatingCamera;

public enum VideoShape
{
    Circle = 0,
    RoundedSquare = 1,
    Square = 2,
}

public enum DisplayRatio
{
    Auto = 0,
    Square1x1 = 1,
    Ratio4x3 = 2,
    Ratio16x9 = 3,
}

/// <summary>每个悬浮相机窗口的配置，作为该窗口与设置面板共享的绑定源。</summary>
public sealed class CameraSettings : INotifyPropertyChanged
{
    private bool _enabled = true;
    private int _deviceIndex;
    private string _deviceName = "";
    private bool _mirror = true;
    private VideoShape _shape = VideoShape.Circle;
    private DisplayRatio _ratio = DisplayRatio.Auto;
    private double _sizePercent = 62;
    private bool _alwaysOnTop = true;
    private bool _lockPosition;
    private string _language = "zh";

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public int DeviceIndex
    {
        get => _deviceIndex;
        set
        {
            if (IsRebuilding) return;
            Set(ref _deviceIndex, value);
        }
    }

    public string DeviceName
    {
        get => _deviceName;
        set => Set(ref _deviceName, value);
    }

    public bool Mirror
    {
        get => _mirror;
        set => Set(ref _mirror, value);
    }

    public VideoShape Shape
    {
        get => _shape;
        set
        {
            if (!Set(ref _shape, value)) return;
            OnChanged(nameof(ShapeIndex));
            OnChanged(nameof(RatioEnabled));
        }
    }

    public int ShapeIndex
    {
        get => (int)_shape;
        set
        {
            if (IsRebuilding) return;
            Shape = (VideoShape)Math.Clamp(value, 0, 2);
        }
    }

    public DisplayRatio Ratio
    {
        get => _ratio;
        set
        {
            if (!Set(ref _ratio, value)) return;
            OnChanged(nameof(RatioIndex));
        }
    }

    public int RatioIndex
    {
        get => (int)_ratio;
        set => Ratio = (DisplayRatio)Math.Clamp(value, 0, 3);
    }

    /// <summary>圆形时比例不可用，与参考软件一致。</summary>
    public bool RatioEnabled => _shape != VideoShape.Circle;

    public double SizePercent
    {
        get => _sizePercent;
        set
        {
            var v = Math.Clamp(value, 20, 100);
            if (!Set(ref _sizePercent, v)) return;
            OnChanged(nameof(SizeText));
        }
    }

    public string SizeText => $"{(int)_sizePercent}%";

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => Set(ref _alwaysOnTop, value);
    }

    public bool LockPosition
    {
        get => _lockPosition;
        set => Set(ref _lockPosition, value);
    }

    public string Language
    {
        get => _language;
        set
        {
            if (!Set(ref _language, value)) return;
            OnChanged(nameof(LanguageIndex));
            Loc.I.Lang = value;
        }
    }

    public int LanguageIndex
    {
        get => _language == "en" ? 1 : 0;
        set
        {
            if (IsRebuilding) return;
            Language = value == 1 ? "en" : "zh";
        }
    }

    /// <summary>由全局语言驱动，避免触发 Loc 再次回写。</summary>
    public void SyncLanguage(string lang)
    {
        if (_language == lang) return;
        _language = lang;
        OnChanged(nameof(Language));
        OnChanged(nameof(LanguageIndex));
    }

    private int _rebuildDepth;

    private bool IsRebuilding => _rebuildDepth > 0;

    /// <summary>重建下拉框选项时挂起回写，避免清空集合导致选中项被重置为 -1。</summary>
    public void BeginOptionRebuild() => _rebuildDepth++;

    public void EndOptionRebuild()
    {
        if (_rebuildDepth > 0) _rebuildDepth--;
        if (_rebuildDepth != 0) return;

        OnChanged(nameof(DeviceIndex));
        OnChanged(nameof(ShapeIndex));
        OnChanged(nameof(RatioIndex));
        OnChanged(nameof(LanguageIndex));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnChanged(name);
        return true;
    }

    private void OnChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
