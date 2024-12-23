using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenCvSharp;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace IPStreamApp
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;
        private WriteableBitmap? _bitmap;
        private WasapiLoopbackCapture? _captureAudio; // Windows
        private WaveOutEvent? _waveOut; // Windows and Linux
        private WaveInEvent? _waveIn; // Linux

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

            if (_capture.Open(url))
            {
                _isStreaming = true;
                ConnectButton.IsEnabled = false;
                DisconnectButton.IsEnabled = true;
                CaptureFrameButton.IsEnabled = true;

                InitializeAudioCapture();

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

            StopAudioCapture();

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
            using var frame = new Mat();
            while (_isStreaming && _capture != null && _capture.IsOpened())
            {
                if (_capture.Read(frame) && !frame.Empty())
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateBitmap(frame);
                        VideoDisplay.InvalidateVisual();
                    });
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
                            Marshal.WriteByte(dstPtr, index + 3, 255); // A
                            index += 4;
                        }
                        srcPtr += rowBytes;
                        dstPtr += lockedBitmap.RowBytes;
                    }
                }
                else
                {
                    int imageSize = width * height * bytesPerPixel;
                    byte[] managedArray = new byte[imageSize];

                    Marshal.Copy(srcPtr, managedArray, 0, imageSize);

                    for (int i = 0; i < imageSize; i += bytesPerPixel)
                    {
                        Marshal.WriteByte(dstPtr, i / bytesPerPixel * 4 + 0, managedArray[i + 0]); // B
                        Marshal.WriteByte(dstPtr, i / bytesPerPixel * 4 + 1, managedArray[i + 1]); // G
                        Marshal.WriteByte(dstPtr, i / bytesPerPixel * 4 + 2, managedArray[i + 2]); // R

                        if (bytesPerPixel == 4)
                        {
                            Marshal.WriteByte(dstPtr, i / bytesPerPixel * 4 + 3, managedArray[i + 3]); // A
                        }
                        else
                        {
                            Marshal.WriteByte(dstPtr, i / bytesPerPixel * 4 + 3, 255); // A
                        }
                    }
                }
            }
        }


        private void InitializeAudioCapture()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _captureAudio = new WasapiLoopbackCapture();
                _waveOut = new WaveOutEvent();
                var waveProvider = new BufferedWaveProvider(_captureAudio.WaveFormat);
                _waveOut.Init(waveProvider);

                _captureAudio.DataAvailable += (s, e) =>
                {
                    waveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };

                _captureAudio.StartRecording();
                _waveOut.Play();
            }
            else
            {
                _waveIn = new WaveInEvent();
                _waveIn.DeviceNumber = -1; // Устройство по умолчанию
                _waveIn.WaveFormat = new WaveFormat(44100, 16, 2); // 44.1 кГц, 16 бит, стерео

                var bufferedWaveProvider = new BufferedWaveProvider(_waveIn.WaveFormat);

                _waveIn.DataAvailable += (s, e) =>
                {
                    bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };

                _waveOut = new WaveOutEvent();
                _waveOut.Init(bufferedWaveProvider);

                _waveIn.StartRecording();
                _waveOut.Play();
            }
        }

        private void StopAudioCapture()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;

                _captureAudio?.StopRecording();
                _captureAudio?.Dispose();
                _captureAudio = null;
            }
            else
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;

                _waveIn?.StopRecording();
                _waveIn?.Dispose();
                _waveIn = null;
            }
        }
    }
}








