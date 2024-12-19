using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenCvSharp;

namespace IPStreamApp
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;
        private WriteableBitmap? _bitmap;

        public MainWindow()
        {
            InitializeComponent();
            DisconnectButton.IsEnabled = false;
            CaptureFrameButton.IsEnabled = false;
        }

        private async void OnConnectClick(object? sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text;
            if (string.IsNullOrWhiteSpace(url))
            {
                SimpleMessageBox.Show(this, "Пожалуйста, введите URL.");
                return;
            }

            _capture = new VideoCapture(url);
            if (_capture.IsOpened())
            {
                _isStreaming = true;
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                CaptureFrameButton.IsEnabled = true;
                await UpdateFrameAsync();
            }
            else
            {
                SimpleMessageBox.Show(this, "Не удалось подключиться к камере.");
            }
        }

        private void OnDisconnectClick(object? sender, RoutedEventArgs e)
        {
            _isStreaming = false;
            _capture?.Release();
            _capture = null;

            VideoDisplay.Source = null;
            _bitmap = null;

            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            CaptureFrameButton.IsEnabled = false;
        }

        private async Task UpdateFrameAsync()
        {
            while (_isStreaming && _capture != null && _capture.IsOpened())
            {
                using var frame = new Mat();
                _capture.Read(frame);

                if (!frame.Empty())
                {
                    UpdateBitmap(frame);
                    VideoDisplay.InvalidateVisual();
                }

                await Task.Delay(42);
            }
        }

        private void OnCaptureFrameClick(object? sender, RoutedEventArgs e)
        {
            if (_capture != null && _capture.IsOpened())
            {
                using var frame = new Mat();
                _capture.Read(frame);

                if (!frame.Empty())
                {
                    SaveFrameAsBmp(frame);
                    SimpleMessageBox.Show(this, "Кадр успешно сохранён.");
                }
            }
        }

        private void SaveFrameAsBmp(Mat frame)
        {
            string folderName = "savedframes";
            string filePath = Path.Combine(folderName, $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.bmp");

            if (!Directory.Exists(folderName))
            {
                Directory.CreateDirectory(folderName);
            }

            frame.SaveImage(filePath);
        }

        private void UpdateBitmap(Mat frame)
        {
            if (frame.Type() != MatType.CV_8UC3 && frame.Type() != MatType.CV_8UC4)
            {
                throw new ArgumentException("Mat must be of type CV_8UC3 or CV_8UC4");
            }

            int width = frame.Width;
            int height = frame.Height;
            int bytesPerPixel = frame.Type() == MatType.CV_8UC3 ? 3 : 4;
            int stride = width * 4;

            if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
                VideoDisplay.Source = _bitmap;
            }

            using (var lockedBitmap = _bitmap.Lock())
            {
                IntPtr srcPtr = frame.Data;
                IntPtr dstPtr = lockedBitmap.Address;

                if (bytesPerPixel == 3)
                {
                    int rowBytes = width * bytesPerPixel;
                    byte[] managedArray = new byte[rowBytes];
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(srcPtr, managedArray, 0, rowBytes);

                        int index = 0;
                        for (int x = 0; x < width; x++)
                        {
                            Marshal.WriteByte(dstPtr, index + 0, managedArray[x * 3 + 0]); // B
                            Marshal.WriteByte(dstPtr, index + 1, managedArray[x * 3 + 1]); // G
                            Marshal.WriteByte(dstPtr, index + 2, managedArray[x * 3 + 2]); // R
                            Marshal.WriteByte(dstPtr, index + 3, 255);                    // A
                            index += 4;
                        }

                        srcPtr += rowBytes;
                        dstPtr += lockedBitmap.RowBytes;
                    }
                }
                else
                {
                    int imageSize = (int)(lockedBitmap.RowBytes * height);
                    byte[] managedArray = new byte[imageSize];

                    Marshal.Copy(srcPtr, managedArray, 0, imageSize);
                    Marshal.Copy(managedArray, 0, dstPtr, imageSize);
                }
            }
        }
    }
}






