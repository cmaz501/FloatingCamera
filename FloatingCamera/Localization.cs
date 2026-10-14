using System.ComponentModel;

namespace FloatingCamera;

/// <summary>
/// 极简本地化：以 key 索引字典，绑定语法 Binding Path=[Key], Source={x:Static loc:Loc.I}。
/// 切换语言时触发 "Item[]" 通知，所有绑定会自动刷新。
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new Loc();

    private static readonly Dictionary<string, Dictionary<string, string>> Table = new()
    {
        ["zh"] = new Dictionary<string, string>
        {
            ["Enable"] = "开启",
            ["NewWindow"] = "新建相机窗口",
            ["Device"] = "摄像头设备",
            ["Mirror"] = "镜像展示",
            ["Shape"] = "画面形状",
            ["Ratio"] = "显示比例",
            ["Size"] = "画面大小",
            ["Topmost"] = "始终最前",
            ["Lock"] = "锁定位置",
            ["Language"] = "语言",
            ["Unlock"] = "解锁完整版",
            ["SettingsTitle"] = "设置",
            ["On"] = "On",
            ["Off"] = "Off",
            ["ShapeCircle"] = "圆形",
            ["ShapeRounded"] = "圆角方形",
            ["ShapeSquare"] = "方形",
            ["RatioAuto"] = "自动",
            ["Ratio1x1"] = "1:1",
            ["Ratio4x3"] = "4:3",
            ["Ratio16x9"] = "16:9",
            ["TrayShow"] = "显示设置面板",
            ["TrayNew"] = "新建相机窗口",
            ["TrayHide"] = "隐藏全部窗口",
            ["TrayExit"] = "退出",
            ["MenuSettings"] = "设置",
            ["MenuNew"] = "新建窗口",
            ["MenuClose"] = "关闭窗口",
            ["MenuTopmost"] = "始终最前",
            ["NoCamera"] = "未检测到摄像头",
            ["Camera"] = "摄像头",
            ["MsgUnlockedTitle"] = "完整版已解锁",
            ["MsgUnlockedBody"] = "本版本已包含全部功能，无需额外解锁。",
            ["MsgNoCameraTitle"] = "提示",
            ["MsgNoCameraBody"] = "未检测到可用的摄像头设备，请检查设备连接。",
            ["OK"] = "确定",
        },
        ["en"] = new Dictionary<string, string>
        {
            ["Enable"] = "Enable",
            ["NewWindow"] = "New camera window",
            ["Device"] = "Camera device",
            ["Mirror"] = "Mirror",
            ["Shape"] = "Frame shape",
            ["Ratio"] = "Display ratio",
            ["Size"] = "Frame size",
            ["Topmost"] = "Always on top",
            ["Lock"] = "Lock position",
            ["Language"] = "Language",
            ["Unlock"] = "Unlock full version",
            ["SettingsTitle"] = "Settings",
            ["On"] = "On",
            ["Off"] = "Off",
            ["ShapeCircle"] = "Circle",
            ["ShapeRounded"] = "Rounded square",
            ["ShapeSquare"] = "Square",
            ["RatioAuto"] = "Auto",
            ["Ratio1x1"] = "1:1",
            ["Ratio4x3"] = "4:3",
            ["Ratio16x9"] = "16:9",
            ["TrayShow"] = "Show settings",
            ["TrayNew"] = "New camera window",
            ["TrayHide"] = "Hide all windows",
            ["TrayExit"] = "Exit",
            ["MenuSettings"] = "Settings",
            ["MenuNew"] = "New window",
            ["MenuClose"] = "Close window",
            ["MenuTopmost"] = "Always on top",
            ["NoCamera"] = "No camera detected",
            ["Camera"] = "Camera",
            ["MsgUnlockedTitle"] = "Full version unlocked",
            ["MsgUnlockedBody"] = "All features are already available. No unlock required.",
            ["MsgNoCameraTitle"] = "Notice",
            ["MsgNoCameraBody"] = "No available camera device was detected. Please check the connection.",
            ["OK"] = "OK",
        },
    };

    private string _lang = "zh";

    public string Lang
    {
        get => _lang;
        set
        {
            if (_lang == value) return;
            _lang = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lang)));
        }
    }

    public string this[string key]
    {
        get
        {
            if (Table.TryGetValue(_lang, out var t) && t.TryGetValue(key, out var v)) return v;
            if (Table["zh"].TryGetValue(key, out var fallback)) return fallback;
            return key;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
