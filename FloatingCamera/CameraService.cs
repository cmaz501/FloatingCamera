using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace FloatingCamera;

/// <summary>基于 OpenCV / DirectShow 的摄像头采集服务，逐帧投递已冻结的 BitmapSource。</summary>
public sealed class CameraService : IDisposable
{
    private VideoCapture? _capture;
    private Thread? _thread;
    private volatile bool _running;

    public event Action<BitmapSource>? FrameReady;
    public event Action<string>? Failed;

    public bool IsRunning => _running;

    /// <summary>枚举摄像头友好名称（优先 WMI，失败则探测设备索引）。</summary>
    public static List<string> EnumerateDevices()
    {
        var names = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE PNPClass='Camera' OR PNPClass='Image'");
            foreach (ManagementBaseObject mo in searcher.Get())
            {
                var n = mo["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(n) && !names.Contains(n)) names.Add(n);
            }
        }
        catch
        {
            // 某些精简系统无 WMI 权限，回退到探测。
        }

        if (names.Count == 0)
        {
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                    if (probe.IsOpened()) names.Add($"Camera {i + 1}");
                }
                catch
                {
                    // 探测失败继续下一个索引。
                }
            }
        }

        return names;
    }

    public bool Start(int deviceIndex)
    {
        Stop();
        try
        {
            _capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
            if (!_capture.IsOpened())
            {
                _capture.Dispose();
                _capture = null;
                Failed?.Invoke("open-failed");
                return false;
            }

            _capture.Set(VideoCaptureProperties.FrameWidth, 1280);
            _capture.Set(VideoCaptureProperties.FrameHeight, 720);
            _capture.Set(VideoCaptureProperties.BufferSize, 1);

            _running = true;
            _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "CameraCapture" };
            _thread.Start();
            return true;
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex.Message);
            return false;
        }
    }

    private void CaptureLoop()
    {
        var frame = new Mat();
        while (_running)
        {
            try
            {
                var capture = _capture;
                if (capture is null) break;

                if (!capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(20);
                    continue;
                }

                var bitmap = ToBitmapSource(frame);
                if (bitmap is not null) FrameReady?.Invoke(bitmap);
            }
            catch
            {
                Thread.Sleep(30);
            }
        }
    }

    private static BitmapSource? ToBitmapSource(Mat mat)
    {
        var w = mat.Width;
        var h = mat.Height;
        if (w <= 0 || h <= 0) return null;

        var stride = (int)mat.Step();
        var buffer = new byte[stride * h];
        Marshal.Copy(mat.Data, buffer, 0, buffer.Length);

        var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, buffer, stride);
        bitmap.Freeze();
        return bitmap;
    }

    public void Stop()
    {
        _running = false;
        try { _thread?.Join(600); } catch { /* 线程退出超时忽略 */ }
        _thread = null;

        try { _capture?.Release(); } catch { }
        try { _capture?.Dispose(); } catch { }
        _capture = null;
    }

    public void Dispose() => Stop();
}
