using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ManagedBass;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace IPStreamApp
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private VideoCapture? _videoCapture;
        private bool _isStreaming = false;
        private WriteableBitmap? _videoFrameBitmap;
        private int _audioStreamHandle = 0;
        private Process? _ffmpegProcess;

        public MainWindow()
        {
            InitializeComponent();
            InitializeUIState();
        }

        private void InitializeUIState()
        {
            DisconnectButton.IsEnabled = false;
            CaptureFrameButton.IsEnabled = false;
        }

        private async void OnConnectClick(object? sender, RoutedEventArgs e)
        {
            var cameraUrl = UrlTextBox.Text;
            if (string.IsNullOrWhiteSpace(cameraUrl))
            {
                SimpleMessageBox.Show(this, "����������, ������� URL ������.");
                return;
            }

            try
            {
                _videoCapture = new VideoCapture(cameraUrl);

                if (_videoCapture.Open(cameraUrl))
                {
                    _isStreaming = true;
                    ConnectButton.IsEnabled = false;
                    DisconnectButton.IsEnabled = true;
                    CaptureFrameButton.IsEnabled = true;

                    StartAudioCapture(cameraUrl);
                    await StartStreamingVideo();
                }
                else
                {
                    SimpleMessageBox.Show(this, "�� ������� ������������ � ������.");
                }
            }
            catch (Exception ex)
            {
                SimpleMessageBox.Show(this, $"��������� ������ ��� �����������: {ex.Message}");
            }
        }

        private void OnDisconnectClick(object? sender, RoutedEventArgs e)
        {
            _isStreaming = false;
            StopAudioCapture();
            ReleaseVideoCapture();

            VideoDisplay.Source = null;
            _videoFrameBitmap = null;

            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
            CaptureFrameButton.IsEnabled = false;
        }

        private async Task StartStreamingVideo()
        {
            using var frame = new Mat();
            while (_isStreaming && _videoCapture != null && _videoCapture.IsOpened())
            {
                if (_videoCapture.Read(frame) && !frame.Empty())
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateVideoFrame(frame);
                        VideoDisplay.InvalidateVisual();
                    });
                }
                await Task.Delay(42); // �������������� 24 ����� � �������
            }
        }

        private void OnCaptureFrameClick(object? sender, RoutedEventArgs e)
        {
            if (_videoCapture != null && _videoCapture.IsOpened())
            {
                using var frame = new Mat();
                _videoCapture.Read(frame);

                if (!frame.Empty())
                {
                    SaveFrameAsBmp(frame);
                    SimpleMessageBox.Show(this, "���� ������� �������.");
                }
            }
        }

        private void SaveFrameAsBmp(Mat frame)
        {
            string folderName = "savedframes";
            Directory.CreateDirectory(folderName); // ������������� ��������� ������� ����������
            string filePath = Path.Combine(folderName, $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.bmp");
            frame.SaveImage(filePath);
        }

        private void UpdateVideoFrame(Mat frame)
        {
            if (frame.Type() != MatType.CV_8UC3 && frame.Type() != MatType.CV_8UC4)
            {
                throw new ArgumentException("�������������� ������ ������� CV_8UC3 � CV_8UC4");
            }

            int width = frame.Width;
            int height = frame.Height;
            int bytesPerPixel = frame.Type() == MatType.CV_8UC3 ? 3 : 4;
            int stride = width * 4; // ������ ���������� 4 ����� �� ������� ��� Bgra8888

            if (_videoFrameBitmap == null || _videoFrameBitmap.PixelSize.Width != width || _videoFrameBitmap.PixelSize.Height != height)
            {
                _videoFrameBitmap = new WriteableBitmap(
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);

                VideoDisplay.Source = _videoFrameBitmap;
            }

            using (var lockedBitmap = _videoFrameBitmap.Lock())
            {
                IntPtr sourcePointer = frame.Data;
                IntPtr destinationPointer = lockedBitmap.Address;

                // �������������� �� BGR � BGRA
                if (bytesPerPixel == 3)
                {
                    byte[] managedArray = new byte[height * stride];
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int sourceIndex = y * width * bytesPerPixel + x * bytesPerPixel;
                            int destinationIndex = y * stride + x * 4;

                            Marshal.Copy(sourcePointer + sourceIndex, managedArray, destinationIndex, 3); // �������� BGR
                            managedArray[destinationIndex + 3] = 255; // ������������� �����-�����
                        }
                    }
                    Marshal.Copy(managedArray, 0, destinationPointer, managedArray.Length);
                }
                else
                {
                    // ��� CV_8UC4 ������ �������� ������
                    int imageSize = width * height * bytesPerPixel;
                    byte[] managedArray = new byte[imageSize];
                    Marshal.Copy(sourcePointer, managedArray, 0, imageSize);
                    Marshal.Copy(managedArray, 0, destinationPointer, imageSize);
                }
            }
        }

        private void StartAudioCapture(string url)
        {
            try
            {
                // ������ �������� FFmpeg ��� ������� �����������
                _ffmpegProcess = new Process();
                _ffmpegProcess.StartInfo.FileName = "ffmpeg";
                _ffmpegProcess.StartInfo.Arguments = $"-i {url} -f wav -acodec pcm_s16le pipe:1";
                _ffmpegProcess.StartInfo.UseShellExecute = false;
                _ffmpegProcess.StartInfo.RedirectStandardOutput = true;
                _ffmpegProcess.StartInfo.CreateNoWindow = true;

                // ������������� Bass
                if (!Bass.Init())
                {
                    Console.WriteLine($"������ ������������� ManagedBass: {Bass.LastError}");
                    return;
                }

                // �������� ����������� ��� ���������������
                _audioStreamHandle = Bass.CreateStream(44100, 2, BassFlags.Default, StreamProc, IntPtr.Zero);

                if (_audioStreamHandle == 0)
                {
                    Console.WriteLine($"������ �������� �����������: {Bass.LastError}");
                    return;
                }

                // ������ �������� FFmpeg
                _ffmpegProcess.Start();

                // ������ ������ �� ��������� ������ FFmpeg � ��������� ������
                Task.Run(() => ReadAudioStreamFromFFmpeg());

                // ������ ���������������
                Bass.ChannelPlay(_audioStreamHandle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"��������� ������ ��� ������� �����: {ex.Message}");
                StopAudioCapture(); // ��������� ������� ��� ������������� ������
            }
        }

        private async Task ReadAudioStreamFromFFmpeg()
        {
            try
            {
                byte[] buffer = new byte[4096]; // ����� ��� ������ ������
                int bytesRead;

                while (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    bytesRead = await _ffmpegProcess.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        // �������� ������ � Bass
                        Bass.StreamPutData(_audioStreamHandle, buffer, bytesRead);
                    }
                    else
                    {
                        // ���� ������ ���, ������ ��������� �����, ����� �� ��������� ���������
                        await Task.Delay(50);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"������ ��� ������ �����������: {ex.Message}");
                // ��������� ������ ��� ������ ������
            }
        }

        private int StreamProc(int handle, IntPtr buffer, int length, IntPtr user)
        {
            // ����� ��������� ����������� (����� ���������� ������, ���� ������ �������������� ����������)
            return length;
        }

        private void StopAudioCapture()
        {
            if (_audioStreamHandle != 0)
            {
                Bass.ChannelStop(_audioStreamHandle);
                Bass.StreamFree(_audioStreamHandle);
                _audioStreamHandle = 0;
            }

            if (_ffmpegProcess != null)
            {
                try
                {
                    if (!_ffmpegProcess.HasExited)
                    {
                        _ffmpegProcess.Kill();
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"������ ��� ��������� �������� FFmpeg: {ex.Message}");
                }
                finally
                {
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
            }

            Bass.Free();
        }

        private void ReleaseVideoCapture()
        {
            _videoCapture?.Release();
            _videoCapture = null;
        }
    }
}







