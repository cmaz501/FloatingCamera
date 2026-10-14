using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace FloatingCamera;

public partial class CameraWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private readonly CameraService _service = new();

    public CameraSettings Settings { get; }

    public CameraWindow(CameraSettings settings)
    {
        Settings = settings;
        DataContext = settings;
        InitializeComponent();

        _service.FrameReady += OnFrameReady;
        _service.Failed += OnCameraFailed;

        Settings.PropertyChanged += OnSettingsChanged;
        Loaded += OnLoaded;
        Closed += OnClosed;
        SizeChanged += (_, _) => UpdateClip();
        MouseEnter += (_, _) => FadeHoverBar(1);
        MouseLeave += (_, _) => FadeHoverBar(0);
        MouseWheel += OnMouseWheel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySize();
        PlaceInitially();
        SyncTopmost();
        ApplyMirror();
        UpdateClip();
        StartCamera();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _service.Dispose();
        Settings.PropertyChanged -= OnSettingsChanged;
        App.Instance?.OnCameraClosed(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 从 Alt+Tab 列表中隐藏，保持纯悬浮窗语义。
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var ex = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, ex | WsExToolWindow);
    }

    private void PlaceInitially()
    {
        var area = SystemParameters.WorkArea;
        var offset = App.Instance?.Cameras.Count * 28 ?? 0;
        Left = Math.Max(area.Left + 12, area.Right - Width - 24 - offset);
        Top = Math.Max(area.Top + 12, area.Top + 24 + offset);
    }

    // ---------- 尺寸 / 形状 / 比例 ----------

    private void ApplySize()
    {
        var size = 120 + 400 * (Settings.SizePercent / 100.0);
        var aspect = Settings.Ratio switch
        {
            DisplayRatio.Ratio4x3 => 4.0 / 3.0,
            DisplayRatio.Ratio16x9 => 16.0 / 9.0,
            _ => 1.0,
        };

        Width = size;
        Height = size / aspect;
        ClampToScreen();
        UpdateClip();
    }

    private void ClampToScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
    }

    private void UpdateClip()
    {
        var w = VideoHost.ActualWidth;
        var h = VideoHost.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var rect = new Rect(0, 0, w, h);
        VideoHost.Clip = Settings.Shape switch
        {
            VideoShape.Circle => new EllipseGeometry(rect),
            VideoShape.RoundedSquare => new RectangleGeometry(rect, Math.Min(w, h) * 0.16, Math.Min(w, h) * 0.16),
            _ => new RectangleGeometry(rect),
        };
    }

    // ---------- 设置同步 ----------

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CameraSettings.SizePercent):
                ApplySize();
                break;
            case nameof(CameraSettings.Ratio):
                ApplySize();
                break;
            case nameof(CameraSettings.Shape):
                UpdateClip();
                break;
            case nameof(CameraSettings.Mirror):
                ApplyMirror();
                break;
            case nameof(CameraSettings.AlwaysOnTop):
                SyncTopmost();
                break;
            case nameof(CameraSettings.LockPosition):
                FrameBorder.Cursor = Settings.LockPosition ? Cursors.Arrow : Cursors.SizeAll;
                break;
            case nameof(CameraSettings.Enabled):
            case nameof(CameraSettings.DeviceIndex):
                StartCamera();
                break;
            case nameof(CameraSettings.Language):
                UpdatePlaceholder();
                break;
        }
    }

    private void SyncTopmost() => Topmost = Settings.AlwaysOnTop;

    private void ApplyMirror()
    {
        VideoBrush.RelativeTransform = Settings.Mirror
            ? new ScaleTransform(-1, 1, 0.5, 0.5)
            : Transform.Identity;
    }

    // ---------- 摄像头 ----------

    private void StartCamera()
    {
        _service.Stop();
        VideoBrush.ImageSource = null;

        if (!Settings.Enabled)
        {
            UpdatePlaceholder();
            return;
        }

        var devices = App.Instance?.Devices ?? new List<string>();
        if (devices.Count == 0)
        {
            PlaceholderText.Text = Loc.I["NoCamera"];
            PlaceholderText.Visibility = Visibility.Visible;
            return;
        }

        var index = Math.Clamp(Settings.DeviceIndex, 0, devices.Count - 1);
        PlaceholderText.Text = Loc.I["NoCamera"];
        PlaceholderText.Visibility = Visibility.Visible;

        if (!_service.Start(index))
        {
            PlaceholderText.Text = Loc.I["NoCamera"];
            PlaceholderText.Visibility = Visibility.Visible;
        }
    }

    private void OnFrameReady(BitmapSource frame) =>
        Dispatcher.BeginInvoke(() =>
        {
            VideoBrush.ImageSource = frame;
            PlaceholderText.Visibility = Visibility.Collapsed;
        });

    private void OnCameraFailed(string message) =>
        Dispatcher.BeginInvoke(() => PlaceholderText.Visibility = Visibility.Visible);

    private void UpdatePlaceholder()
    {
        if (VideoBrush.ImageSource is null) PlaceholderText.Text = Loc.I["NoCamera"];
    }

    // ---------- 交互 ----------

    private void OnFrameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            App.Instance?.ShowSettings(this);
            return;
        }

        if (Settings.LockPosition) return;
        if (e.ChangedButton != MouseButton.Left) return;

        try { DragMove(); }
        catch { /* 拖动被系统中断时忽略 */ }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Settings.LockPosition) return;
        Settings.SizePercent += e.Delta > 0 ? 4 : -4;
        e.Handled = true;
    }

    private void FadeHoverBar(double to)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(140));
        HoverBar.BeginAnimation(OpacityProperty, anim);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        App.Instance?.ShowSettings(this);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    public void RefreshDeviceList()
    {
        if (Settings.Enabled) StartCamera();
    }
}
