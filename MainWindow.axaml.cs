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
        }

        private async void OnConnectClick(object? sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text;
            if (string.IsNullOrWhiteSpace(url))
                return;

            _capture = new VideoCapture(url);
            if (_capture.IsOpened())
            {
                _isStreaming = true;
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

                await Task.Delay(42); // Задержка между кадрами.
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
            string filePath = $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.bmp";
            frame.SaveImage(filePath);
        }
    }

    public static class BitmapHelper
    {
        public static Bitmap ToAvaloniaBitmap(Mat mat)
        {
            using var ms = new MemoryStream();
            mat.ToMemoryStream(".bmp").CopyTo(ms);
            ms.Position = 0;

            return new Bitmap(ms);
        }
    }

    public class SimpleMessageBox : Avalonia.Controls.Window
    {
        public SimpleMessageBox(string message)
        {
            var textBlock = new TextBlock
            {
                Text = message,
                Margin = new Thickness(10)
            };

            var button = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            button.Click += (sender, e) => Close();

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(button);

            Content = stackPanel;
        }

        public static void Show(Avalonia.Controls.Window owner, string message)
        {
            var messageBox = new SimpleMessageBox(message)
            {
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            messageBox.ShowDialog(owner);
        }
    }
}