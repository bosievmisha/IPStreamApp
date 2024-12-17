using System;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace IPStreamApp
{
    public partial class MainWindow : Avalonia.Controls.Window
    {
        private VideoCapture? _capture;
        private bool _isStreaming = false;

        public MainWindow()
        {
            InitializeComponent();
            // Изначально делаем кнопку "Отключиться" неактивной
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

            // Очистка изображения
            VideoDisplay.Source = null;

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
                    VideoDisplay.Source = BitmapHelper.ToAvaloniaBitmap(frame);
                }

                await Task.Delay(42); // Задержка между кадрами (примерно 24 кадра в секунду).
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
    }
}