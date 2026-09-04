using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlashCap;

namespace DittoBuddy;

public partial class MirrorWindow : Window
{
    private enum WbMode
    {
        AutoWhitePriority,
        Cool,
        Natural
    }

    private CaptureDevice? _captureDevice;
    private int _isRendering;
    private volatile bool _isClosing;

    private double _camRatio = 16.0 / 9.0;
    private double _brightnessMultiplier = 1.0;
    private DispatcherTimer? _toastTimer;

    private WbMode _wbMode = WbMode.AutoWhitePriority;
    private double _gainR = 1.00;
    private double _gainG = 1.00;
    private double _gainB = 1.00;
    private int _awbFrameCounter = 0;

    private readonly byte[] _activeLutR = new byte[256];
    private readonly byte[] _activeLutG = new byte[256];
    private readonly byte[] _activeLutB = new byte[256];

    public MirrorWindow()
    {
        InitializeComponent();
        UpdateLuts();
        SetupContextMenu();

        PreviewMouseWheel += MirrorWindow_MouseWheel;
        Loaded += async (_, _) => await StartCameraAsync();
    }

    private void SetupContextMenu()
    {
        var menu = new ContextMenu();

        var awbItem = new MenuItem { Header = "화이트밸런스: 흰색 우선 (자동)", IsCheckable = true, IsChecked = true };
        var coolItem = new MenuItem { Header = "화이트밸런스: 쿨톤 (더 하얗고 뽀얗게)", IsCheckable = true, IsChecked = false };
        var naturalItem = new MenuItem { Header = "화이트밸런스: 원본 색감 유지", IsCheckable = true, IsChecked = false };

        awbItem.Click += (_, _) =>
        {
            _wbMode = WbMode.AutoWhitePriority;
            awbItem.IsChecked = true;
            coolItem.IsChecked = false;
            naturalItem.IsChecked = false;
            ShowToast("화이트밸런스: 흰색 우선 (자동)");
        };

        coolItem.Click += (_, _) =>
        {
            _wbMode = WbMode.Cool;
            _gainR = 0.85;
            _gainG = 0.98;
            _gainB = 1.28;
            UpdateLuts();
            awbItem.IsChecked = false;
            coolItem.IsChecked = true;
            naturalItem.IsChecked = false;
            ShowToast("화이트밸런스: 쿨톤 / 뽀샤시");
        };

        naturalItem.Click += (_, _) =>
        {
            _wbMode = WbMode.Natural;
            _gainR = 1.00;
            _gainG = 1.00;
            _gainB = 1.00;
            UpdateLuts();
            awbItem.IsChecked = false;
            coolItem.IsChecked = false;
            naturalItem.IsChecked = true;
            ShowToast("화이트밸런스: 원본 색감");
        };

        menu.Items.Add(awbItem);
        menu.Items.Add(coolItem);
        menu.Items.Add(naturalItem);
        ContextMenu = menu;
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
        _toastTimer.Tick += (_, _) =>
        {
            ToastBorder.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };
        _toastTimer.Start();
    }

    private void UpdateLuts()
    {
        for (int i = 0; i < 256; i++)
        {
            double norm = i / 255.0;

            // White balance is a plain linear gain (exact passthrough at gain 1.0).
            double wbR = norm * _gainR, wbG = norm * _gainG, wbB = norm * _gainB;
            double r, g, b;

            if (_brightnessMultiplier >= 1.0)
            {
                // Brightening: plain addition, not a gamma curve or a blend. Both of those scale
                // the *difference* between light and dark pixels — by definition, that's what
                // "losing contrast" means, no matter how evenly the scaling is spread. Adding the
                // same amount to every pixel leaves every difference exactly as large as before;
                // only the very brightest pixels eventually clip to white, same as a real light.
                double offset = (_brightnessMultiplier - 1.0) * 0.5; // 0 at 1.0x, +0.3 at the 1.6x cap
                r = wbR + offset;
                g = wbG + offset;
                b = wbB + offset;
            }
            else
            {
                // Dimming: real light fading out scales everything down uniformly (multiply) —
                // no curve to distort either way.
                r = wbR * _brightnessMultiplier;
                g = wbG * _brightnessMultiplier;
                b = wbB * _brightnessMultiplier;
            }

            _activeLutR[i] = (byte)Math.Clamp((int)(r * 255.0), 0, 255);
            _activeLutG[i] = (byte)Math.Clamp((int)(g * 255.0), 0, 255);
            _activeLutB[i] = (byte)Math.Clamp((int)(b * 255.0), 0, 255);
        }
    }

    private void MirrorWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double step = e.Delta > 0 ? 0.05 : -0.05;
        double newMultiplier = Math.Clamp(_brightnessMultiplier + step, 0.7, 1.6);
        if (Math.Abs(newMultiplier - _brightnessMultiplier) > 0.001)
        {
            _brightnessMultiplier = newMultiplier;
            UpdateLuts();
            ShowToast($"밝기: {(int)Math.Round(_brightnessMultiplier * 100)}%");
        }
    }

    private async Task StartCameraAsync()
    {
        StatusPanel.Visibility = Visibility.Visible;
        StatusText.Text = "카메라 연결 중...";

        try
        {
            (CaptureDeviceDescriptor? descriptor, VideoCharacteristics? characteristics) = await Task.Run<(CaptureDeviceDescriptor?, VideoCharacteristics?)>(() =>
            {
                var devices = new CaptureDevices();
                var descriptors = devices.EnumerateDescriptors().ToList();

                var desc = descriptors.FirstOrDefault(d => d.DeviceType == DeviceTypes.DirectShow && d.Characteristics.Any(c => c.PixelFormat != PixelFormats.Unknown))
                        ?? descriptors.FirstOrDefault(d => d.DeviceType == DeviceTypes.MediaFoundation && d.Characteristics.Any(c => c.PixelFormat != PixelFormats.Unknown))
                        ?? descriptors.FirstOrDefault(d => d.Characteristics.Any(c => c.PixelFormat != PixelFormats.Unknown));

                if (desc == null) return (null, null);

                // Prefer resolution close to 1280x720 for optimal quality and performance
                var chara = desc.Characteristics
                    .Where(c => c.PixelFormat != PixelFormats.Unknown)
                    .OrderBy(c => Math.Abs(c.Width - 1280) + Math.Abs(c.Height - 720))
                    .FirstOrDefault() ?? desc.Characteristics.First();

                return (desc, chara);
            });

            if (_isClosing) return;

            if (descriptor == null || characteristics == null)
            {
                StatusText.Text = "연결된 웹캠을 찾을 수 없습니다.";
                return;
            }

            // Adjust window aspect ratio to match camera
            if (characteristics.Width > 0 && characteristics.Height > 0)
            {
                AdjustWindowToRatio((double)characteristics.Width / characteristics.Height);
            }

            var device = await descriptor.OpenAsync(characteristics, OnFrameArrived);

            if (_isClosing)
            {
                device.Dispose();
                return;
            }

            _captureDevice = device;
            await device.StartAsync();
        }
        catch (Exception)
        {
            if (!_isClosing)
            {
                StatusText.Text = "웹캠에 접근할 수 없습니다.\n(다른 앱에서 사용 중이거나 카메라 권한을 확인해주세요.)";
            }
        }
    }

    private void AdjustWindowToRatio(double ratio)
    {
        if (ratio <= 0.1) return;
        _camRatio = ratio;

        Dispatcher.Invoke(() =>
        {
            double chromeW = ActualWidth > ContentGrid.ActualWidth && ContentGrid.ActualWidth > 0
                ? ActualWidth - ContentGrid.ActualWidth
                : 16;
            double chromeH = ActualHeight > ContentGrid.ActualHeight && ContentGrid.ActualHeight > 0
                ? ActualHeight - ContentGrid.ActualHeight
                : 39;

            double targetClientW = 540;
            double targetClientH = targetClientW / ratio;

            Width = targetClientW + chromeW;
            Height = targetClientH + chromeH;

            MinWidth = 240 + chromeW;
            MinHeight = (240 / ratio) + chromeH;
        });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZING = 0x0214;
        const int WMSZ_TOP = 3;
        const int WMSZ_TOPLEFT = 4;
        const int WMSZ_TOPRIGHT = 5;
        const int WMSZ_BOTTOM = 6;

        if (msg == WM_SIZING && _camRatio > 0)
        {
            var rect = Marshal.PtrToStructure<RECT>(lParam);

            double chromeW = ActualWidth > ContentGrid.ActualWidth && ContentGrid.ActualWidth > 0
                ? ActualWidth - ContentGrid.ActualWidth
                : 16;
            double chromeH = ActualHeight > ContentGrid.ActualHeight && ContentGrid.ActualHeight > 0
                ? ActualHeight - ContentGrid.ActualHeight
                : 39;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            int edge = wParam.ToInt32();
            if (edge == WMSZ_TOP || edge == WMSZ_BOTTOM)
            {
                double clientH = Math.Max(10, height - chromeH);
                int newW = (int)Math.Round(clientH * _camRatio + chromeW);
                rect.Right = rect.Left + newW;
            }
            else
            {
                double clientW = Math.Max(10, width - chromeW);
                int newH = (int)Math.Round(clientW / _camRatio + chromeH);
                if (edge == WMSZ_TOPLEFT || edge == WMSZ_TOPRIGHT || edge == WMSZ_TOP)
                {
                    rect.Top = rect.Bottom - newH;
                }
                else
                {
                    rect.Bottom = rect.Top + newH;
                }
            }

            Marshal.StructureToPtr(rect, lParam, true);
            handled = true;
            return (IntPtr)1;
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private void OnFrameArrived(PixelBufferScope bufferScope)
    {
        if (_isClosing) return;

        // Drop frame if previous frame is still being processed or rendered
        if (Interlocked.CompareExchange(ref _isRendering, 1, 0) == 0)
        {
            try
            {
                byte[] image = bufferScope.Buffer.ExtractImage();

                // White Priority Auto White Balance calculation
                if (_wbMode == WbMode.AutoWhitePriority && (++_awbFrameCounter % 6 == 0))
                {
                    UpdateWhitePriorityAwb(image);
                }

                ApplyColorGain(image);

                using var ms = new MemoryStream(image);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze(); // allows cross-thread use

                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (_isClosing) return;
                        CameraImage.Source = bitmap;
                        if (StatusPanel.Visibility != Visibility.Collapsed)
                        {
                            StatusPanel.Visibility = Visibility.Collapsed;
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isRendering, 0);
                    }
                });
            }
            catch
            {
                Interlocked.Exchange(ref _isRendering, 0);
            }
        }
    }

    private void UpdateWhitePriorityAwb(byte[] image)
    {
        if (image.Length < 54 || image[0] != 0x42 || image[1] != 0x4D) return;

        int pixelOffset = BitConverter.ToInt32(image, 10);
        short bpp = BitConverter.ToInt16(image, 28);
        int width = BitConverter.ToInt32(image, 18);
        int height = Math.Abs(BitConverter.ToInt32(image, 22));

        if (pixelOffset <= 0 || width <= 0 || height <= 0) return;

        int bytesPerPixel = bpp / 8;
        if (bytesPerPixel < 3) return;

        int stride = ((width * bytesPerPixel + 3) / 4) * 4;

        long sumR = 0, sumG = 0, sumB = 0;
        int countAll = 0;

        long whiteSumR = 0, whiteSumG = 0, whiteSumB = 0;
        int countWhite = 0;

        int stepY = Math.Max(1, height / 25);
        int stepX = Math.Max(1, width / 35);

        for (int y = 0; y < height; y += stepY)
        {
            int rowStart = pixelOffset + y * stride;
            for (int x = 0; x < width; x += stepX)
            {
                int idx = rowStart + x * bytesPerPixel;
                if (idx + 2 >= image.Length) continue;

                byte b = image[idx];
                byte g = image[idx + 1];
                byte r = image[idx + 2];

                int lum = (r * 299 + g * 587 + b * 114) / 1000;

                sumR += r;
                sumG += g;
                sumB += b;
                countAll++;

                // White Priority: highlight pixels (e.g. white clothes, walls, papers)
                // Filter between 150 and 246 (exclude blown-out sensor clips >= 247)
                if (lum >= 150 && lum <= 246)
                {
                    int maxDiff = Math.Max(Math.Abs(r - g), Math.Max(Math.Abs(r - b), Math.Abs(g - b)));
                    if (maxDiff < 65)
                    {
                        whiteSumR += r;
                        whiteSumG += g;
                        whiteSumB += b;
                        countWhite++;
                    }
                }
            }
        }

        double targetR, targetG, targetB;

        if (countWhite >= 15)
        {
            double avgWhiteR = (double)whiteSumR / countWhite;
            double avgWhiteG = (double)whiteSumG / countWhite;
            double avgWhiteB = (double)whiteSumB / countWhite;

            double target = (avgWhiteR + avgWhiteG + avgWhiteB) / 3.0;

            targetR = target / Math.Max(1.0, avgWhiteR);
            targetG = target / Math.Max(1.0, avgWhiteG);
            targetB = target / Math.Max(1.0, avgWhiteB);
        }
        else if (countAll > 0)
        {
            double avgR = (double)sumR / countAll;
            double avgG = (double)sumG / countAll;
            double avgB = (double)sumB / countAll;

            double avgAll = (avgR + avgG + avgB) / 3.0;

            targetR = avgAll / Math.Max(1.0, avgR);
            targetG = avgAll / Math.Max(1.0, avgG);
            targetB = avgAll / Math.Max(1.0, avgB);
        }
        else
        {
            return;
        }

        // Symmetric clamps: correct toward gray in either direction, not just "de-orange".
        // The old clamps only ever let red go down and blue go up, so anything that wasn't
        // warm-lit (daylight, cool LEDs, shade) stayed permanently color-shifted.
        targetR = Math.Clamp(targetR, 0.7, 1.3);
        targetG = Math.Clamp(targetG, 0.85, 1.15);
        targetB = Math.Clamp(targetB, 0.7, 1.3);

        // Smoothly interpolate gains (EMA) — faster than before so it actually converges
        _gainR = _gainR * 0.7 + targetR * 0.3;
        _gainG = _gainG * 0.7 + targetG * 0.3;
        _gainB = _gainB * 0.7 + targetB * 0.3;

        UpdateLuts();
    }

    private void ApplyColorGain(byte[] image)
    {
        if (image.Length < 54) return;
        // Verify 'BM' header
        if (image[0] != 0x42 || image[1] != 0x4D) return;

        int pixelOffset = BitConverter.ToInt32(image, 10);
        short bpp = BitConverter.ToInt16(image, 28);

        if (pixelOffset <= 0 || pixelOffset >= image.Length) return;

        int bytesPerPixel = bpp switch { 24 => 3, 32 => 4, _ => 0 };
        if (bytesPerPixel == 0) return;

        int limit = image.Length - (bytesPerPixel - 1);
        for (int i = pixelOffset; i < limit; i += bytesPerPixel)
        {
            image[i] = _activeLutB[image[i]];
            image[i + 1] = _activeLutG[image[i + 1]];
            image[i + 2] = _activeLutR[image[i + 2]];
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
        StopCamera();
    }

    private void StopCamera()
    {
        var dev = Interlocked.Exchange(ref _captureDevice, null);
        if (dev != null)
        {
            Task.Run(async () =>
            {
                try
                {
                    await dev.StopAsync();
                    dev.Dispose();
                }
                catch
                {
                }
            });
        }
    }
}
