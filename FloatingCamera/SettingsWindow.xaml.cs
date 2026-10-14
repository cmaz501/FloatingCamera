using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace FloatingCamera;

public partial class SettingsWindow : Window
{
    private CameraWindow? _target;

    public ObservableCollection<string> DeviceOptions { get; } = new();
    public ObservableCollection<string> ShapeOptions { get; } = new();
    public ObservableCollection<string> RatioOptions { get; } = new();
    public ObservableCollection<string> LanguageOptions { get; } = new();

    public SettingsWindow()
    {
        InitializeComponent();
        RebuildOptions();
        Loc.I.PropertyChanged += OnLanguageChanged;
        Closed += (_, _) => Loc.I.PropertyChanged -= OnLanguageChanged;
    }

    /// <summary>把设置面板切换到指定悬浮窗。</summary>
    public void SetTarget(CameraWindow camera)
    {
        _target = camera;
        DataContext = camera.Settings;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Item[]" or nameof(Loc.Lang)) RebuildOptions();
    }

    private void RebuildOptions()
    {
        var settings = DataContext as CameraSettings;
        settings?.BeginOptionRebuild();
        try
        {
            ShapeOptions.Clear();
            ShapeOptions.Add(Loc.I["ShapeCircle"]);
            ShapeOptions.Add(Loc.I["ShapeRounded"]);
            ShapeOptions.Add(Loc.I["ShapeSquare"]);

            RatioOptions.Clear();
            RatioOptions.Add(Loc.I["RatioAuto"]);
            RatioOptions.Add(Loc.I["Ratio1x1"]);
            RatioOptions.Add(Loc.I["Ratio4x3"]);
            RatioOptions.Add(Loc.I["Ratio16x9"]);

            LanguageOptions.Clear();
            LanguageOptions.Add("简体中文");
            LanguageOptions.Add("English");

            var devices = App.Instance?.Devices ?? new List<string>();

            DeviceOptions.Clear();
            if (devices.Count == 0)
            {
                DeviceOptions.Add(Loc.I["NoCamera"]);
            }
            else
            {
                foreach (var d in devices) DeviceOptions.Add(d);
            }

            if (settings is not null)
            {
                settings.DeviceIndex = Math.Clamp(
                    settings.DeviceIndex, 0, Math.Max(0, DeviceOptions.Count - 1));
            }
        }
        finally
        {
            settings?.EndOptionRebuild();
        }
    }

    /// <summary>摄像头热插拔后刷新设备列表。</summary>
    public void RefreshDevices()
    {
        RebuildOptions();
        _target?.RefreshDeviceList();
    }

    private void OnNewWindowClick(object sender, RoutedEventArgs e)
    {
        App.Instance?.CreateCameraWindow();
    }

    private void OnUnlockClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, Loc.I["MsgUnlockedBody"], Loc.I["MsgUnlockedTitle"],
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
