using Avalonia.Controls;
using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPStreamApp
{
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
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
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
