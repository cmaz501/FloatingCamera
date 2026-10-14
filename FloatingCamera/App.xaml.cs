using System.ComponentModel;
using System.IO;
using System.Management;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace FloatingCamera;

public partial class App : Application
{
    private Mutex? _mutex;
    private Forms.NotifyIcon? _tray;
    private Forms.ContextMenuStrip? _trayMenu;
    private ManagementEventWatcher? _deviceWatcher;
    private SettingsWindow? _settings;
    private readonly List<CameraWindow> _cameras = new();

    public static App Instance => (App)Current!;

    public IReadOnlyList<CameraWindow> Cameras => _cameras;

    public List<string> Devices { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "floatingcam_error.log"),
                    $"[{DateTime.Now:O}] {args.Exception}\n\n");
            }
            catch { }
            args.Handled = true;
        };

        _mutex = new Mutex(true, "FloatingCamera_SingleInstance_Mutex", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        RefreshDevices();
        CreateTray();
        CreateCameraWindow();
        ShowSettings(_cameras.Count > 0 ? _cameras[0] : null);
        StartDeviceWatcher();

        if (e.Args.Contains("--selftest")) RunSelfTest();
        else if (e.Args.Contains("--shot")) ScheduleShot();
    }

    /// <summary>诊断用：按顺序切换各项设置并逐个截图，验证功能是否真正生效。</summary>
    private void RunSelfTest()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "shots");
        Directory.CreateDirectory(dir);
        var log = new System.Text.StringBuilder();
        var steps = new Queue<Action>();

        steps.Enqueue(() =>
        {
            SaveShot(_settings!, Path.Combine(dir, "settings_zh.png"));
            SaveShot(_cameras[0], Path.Combine(dir, "cam1_circle.png"));
            log.AppendLine($"[1] shape={_cameras[0].Settings.Shape} ratio={_cameras[0].Settings.Ratio} " +
                           $"win={_cameras[0].ActualWidth:F0}x{_cameras[0].ActualHeight:F0} topmost={_cameras[0].Topmost} " +
                           $"clip={ClipOf(_cameras[0])}");
            _cameras[0].Settings.Shape = VideoShape.RoundedSquare;
        });

        steps.Enqueue(() =>
        {
            SaveShot(_cameras[0], Path.Combine(dir, "cam2_rounded.png"));
            log.AppendLine($"[2] shape={_cameras[0].Settings.Shape} clip={ClipOf(_cameras[0])}");
            _cameras[0].Settings.Shape = VideoShape.Square;
            _cameras[0].Settings.Ratio = DisplayRatio.Ratio16x9;
        });

        steps.Enqueue(() =>
        {
            SaveShot(_cameras[0], Path.Combine(dir, "cam3_wide.png"));
            log.AppendLine($"[3] shape={_cameras[0].Settings.Shape} ratio={_cameras[0].Settings.Ratio} " +
                           $"win={_cameras[0].ActualWidth:F0}x{_cameras[0].ActualHeight:F0} clip={ClipOf(_cameras[0])}");
            _cameras[0].Settings.SizePercent = 90;
            _cameras[0].Settings.Mirror = false;
        });

        steps.Enqueue(() =>
        {
            SaveShot(_cameras[0], Path.Combine(dir, "cam4_big_nomirror.png"));
            log.AppendLine($"[4] size={_cameras[0].Settings.SizePercent}% " +
                           $"win={_cameras[0].ActualWidth:F0}x{_cameras[0].ActualHeight:F0} " +
                           $"mirror={_cameras[0].Settings.Mirror} brushTransform={TransformOf(_cameras[0])}");
            _cameras[0].Settings.AlwaysOnTop = false;
            _cameras[0].Settings.LockPosition = true;
        });

        steps.Enqueue(() =>
        {
            log.AppendLine($"[5] topmost={_cameras[0].Topmost} lock={_cameras[0].Settings.LockPosition}");
            _cameras[0].Settings.AlwaysOnTop = true;
            _cameras[0].Settings.LockPosition = false;
            CreateCameraWindow();
            log.AppendLine($"[6] newWindow -> cameras={_cameras.Count}");
        });

        steps.Enqueue(() =>
        {
            if (_cameras.Count > 1)
            {
                SaveShot(_cameras[1], Path.Combine(dir, "cam5_second.png"));
                var src = (_cameras[1].FindName("VideoBrush") as ImageBrush)?.ImageSource;
                log.AppendLine($"[7] cam2 win={_cameras[1].ActualWidth:F0}x{_cameras[1].ActualHeight:F0} " +
                               $"frame={(src is BitmapSource b ? $"{b.PixelWidth}x{b.PixelHeight}" : "none")}");
            }
            Loc.I.Lang = "en";
        });

        steps.Enqueue(() =>
        {
            SaveShot(_settings!, Path.Combine(dir, "settings_en.png"));
            log.AppendLine($"[8] title={_settings!.Title} shapeOptions={string.Join("|", _settings.ShapeOptions)} " +
                           $"ratioOptions={string.Join("|", _settings.RatioOptions)}");
            log.AppendLine($"[9] trayItems={TraySummary()}");
            File.WriteAllText(Path.Combine(dir, "selftest.txt"), log.ToString());
            Shutdown();
        });

        var start = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        var step = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        start.Tick += (_, _) => { start.Stop(); step.Start(); };
        step.Tick += (_, _) =>
        {
            if (steps.Count == 0) { step.Stop(); return; }
            try { steps.Dequeue()(); }
            catch (Exception ex) { log.AppendLine($"[ERR] {ex.GetType().Name}: {ex.Message}"); }
        };
        start.Start();
    }

    private static string ClipOf(CameraWindow w) =>
        (w.FindName("VideoHost") as FrameworkElement)?.Clip?.ToString() ?? "null";

    private static string TransformOf(CameraWindow w) =>
        (w.FindName("VideoBrush") as ImageBrush)?.RelativeTransform?.ToString() ?? "null";

    private string TraySummary() =>
        _trayMenu is null
            ? "null"
            : string.Join(",", _trayMenu.Items.OfType<Forms.ToolStripMenuItem>().Select(i => i.Text));

    /// <summary>诊断用：把各窗口的视觉树直接渲染为 PNG，避免屏幕遮挡导致的截图失真。</summary>
    private void ScheduleShot()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var dir = Path.Combine(AppContext.BaseDirectory, "shots");
            Directory.CreateDirectory(dir);
            if (_settings is not null) SaveShot(_settings, Path.Combine(dir, "settings_zh.png"));
            var diag = new System.Text.StringBuilder();
            for (var i = 0; i < _cameras.Count; i++)
            {
                SaveShot(_cameras[i], Path.Combine(dir, $"camera{i}.png"));
                if (_cameras[i].FindName("VideoBrush") is ImageBrush ib
                    && ib.ImageSource is BitmapSource bs)
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(bs));
                    using var fs = File.Create(Path.Combine(dir, $"frame{i}.png"));
                    enc.Save(fs);

                    diag.AppendLine($"cam{i}: src={bs.PixelWidth}x{bs.PixelHeight} dpi={bs.DpiX} stretch={ib.Stretch}");
                }
                if (_cameras[i].FindName("VideoHost") is FrameworkElement host)
                {
                    diag.AppendLine($"  host={host.ActualWidth}x{host.ActualHeight} clip={host.Clip}");
                }
                diag.AppendLine($"  win={_cameras[i].ActualWidth}x{_cameras[i].ActualHeight}");
            }
            File.WriteAllText(Path.Combine(dir, "diag.txt"), diag.ToString());

            if (_settings is null)
            {
                Shutdown();
                return;
            }

            // 切到英文再截一张，用于验证语言刷新（标题/选项/托盘）。
            Loc.I.Lang = "en";
            var en = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            en.Tick += (_, _) =>
            {
                en.Stop();
                SaveShot(_settings, Path.Combine(dir, "settings_en.png"));
                File.AppendAllText(Path.Combine(dir, "diag.txt"),
                    $"lang={Loc.I.Lang} shapeIndex={_cameras[0].Settings.ShapeIndex} " +
                    $"ratioIndex={_cameras[0].Settings.RatioIndex}\n" +
                    $"shapeOptions={string.Join("|", _settings.ShapeOptions)}\n" +
                    $"ratioOptions={string.Join("|", _settings.RatioOptions)}\n" +
                    $"deviceOptions={string.Join("|", _settings.DeviceOptions)}\n" +
                    $"title={_settings.Title}\n");
                Shutdown();
            };
            en.Start();
        };
        timer.Start();
    }

    private static void SaveShot(Window window, string path)
    {
        if (window.Content is not FrameworkElement root) return;
        var w = (int)Math.Ceiling(root.ActualWidth);
        var h = (int)Math.Ceiling(root.ActualHeight);
        if (w <= 0 || h <= 0) return;

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _deviceWatcher?.Stop(); } catch { }
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ---------- 设备 ----------

    public void RefreshDevices() => Devices = CameraService.EnumerateDevices();

    private void StartDeviceWatcher()
    {
        try
        {
            const string query =
                "SELECT * FROM __InstanceOperationEvent WITHIN 3 " +
                "WHERE TargetInstance ISA 'Win32_PnPEntity' " +
                "AND (TargetInstance.PNPClass='Camera' OR TargetInstance.PNPClass='Image')";

            _deviceWatcher = new ManagementEventWatcher(new WqlEventQuery(query));
            _deviceWatcher.EventArrived += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                RefreshDevices();
                _settings?.RefreshDevices();
            });
            _deviceWatcher.Start();
        }
        catch
        {
            // 无 WMI 权限时静默跳过热插拔监听。
        }
    }

    // ---------- 窗口 ----------

    public CameraWindow CreateCameraWindow()
    {
        var settings = new CameraSettings
        {
            Language = Loc.I.Lang,
            DeviceIndex = _cameras.Count > 0 ? _cameras[0].Settings.DeviceIndex : 0,
        };

        var camera = new CameraWindow(settings);
        _cameras.Add(camera);
        camera.Show();
        return camera;
    }

    public void ShowSettings(CameraWindow? target)
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
        }

        if (target is not null) _settings.SetTarget(target);
        else if (_settings.DataContext is null && _cameras.Count > 0) _settings.SetTarget(_cameras[0]);

        foreach (var camera in _cameras)
        {
            if (!camera.IsVisible) camera.Show();
        }

        _settings.Show();
        _settings.Activate();
    }

    public void OnCameraClosed(CameraWindow camera)
    {
        _cameras.Remove(camera);

        if (_settings is null) return;

        if (_cameras.Count == 0)
        {
            _settings.Close();
        }
        else if (ReferenceEquals(_settings.DataContext, camera.Settings))
        {
            _settings.SetTarget(_cameras[^1]);
        }
    }

    private void HideAll()
    {
        foreach (var camera in _cameras) camera.Hide();
        _settings?.Hide();
    }

    private void ExitApp()
    {
        if (_tray is not null) _tray.Visible = false;
        Shutdown();
    }

    // ---------- 托盘 ----------

    private void CreateTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "悬浮摄像头",
            Visible = true,
        };

        _trayMenu = new Forms.ContextMenuStrip();
        _tray.ContextMenuStrip = _trayMenu;
        BuildTrayMenu();

        _tray.DoubleClick += (_, _) => ShowSettings(null);
        Loc.I.PropertyChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "Item[]" or nameof(Loc.Lang)) BuildTrayMenu();
    }

    private void BuildTrayMenu()
    {
        if (_trayMenu is null) return;

        _trayMenu.Items.Clear();
        _trayMenu.Items.Add(new Forms.ToolStripMenuItem(Loc.I["TrayShow"], null, (_, _) => ShowSettings(null)));
        _trayMenu.Items.Add(new Forms.ToolStripMenuItem(Loc.I["TrayNew"], null, (_, _) => CreateCameraWindow()));
        _trayMenu.Items.Add(new Forms.ToolStripMenuItem(Loc.I["TrayHide"], null, (_, _) => HideAll()));
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add(new Forms.ToolStripMenuItem(Loc.I["TrayExit"], null, (_, _) => ExitApp()));

        if (_tray is not null) _tray.Text = Loc.I["TrayShow"];
    }

    private static Drawing.Icon LoadAppIcon()
    {
        // 单文件发布时不会有独立的 Assets 目录，直接从 exe 已嵌入的应用图标中提取。
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is not null) return icon;
            }
        }
        catch
        {
            // 提取失败时回退到系统默认图标。
        }

        return Drawing.SystemIcons.Application;
    }
}
