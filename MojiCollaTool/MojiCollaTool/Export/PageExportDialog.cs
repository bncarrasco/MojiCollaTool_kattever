using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MojiCollaTool.Export
{
    internal sealed class PageExportDialog : Window
    {
        private readonly TextBox _directoryTextBox;
        private readonly TextBox _prefixTextBox;
        private readonly RadioButton _currentRadioButton;
        private readonly RadioButton _allRadioButton;
        private readonly ComboBox _formatComboBox;

        public PageExportDialog(string initialDirectory, PageExportScope initialScope)
        {
            Title = "画像一括出力";
            Width = 480;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            _directoryTextBox = new TextBox { Text = initialDirectory ?? string.Empty, Margin = new Thickness(0, 4, 0, 8) };
            _prefixTextBox = new TextBox { Text = "MojiColla", Margin = new Thickness(0, 4, 0, 8) };
            _currentRadioButton = new RadioButton { Content = "現在のページ", IsChecked = initialScope == PageExportScope.CurrentPage, Margin = new Thickness(0, 4, 12, 4) };
            _allRadioButton = new RadioButton { Content = "全ページ", IsChecked = initialScope == PageExportScope.AllPages, Margin = new Thickness(0, 4, 12, 4) };
            _formatComboBox = new ComboBox { Margin = new Thickness(0, 4, 0, 8), ItemsSource = new[] { "PNG（透明背景を保持）", "JPEG（白背景）" }, SelectedIndex = 0 };

            var root = new StackPanel { Margin = new Thickness(14) };
            root.Children.Add(new TextBlock { Text = "出力先フォルダー" });
            var directoryPanel = new DockPanel();
            var browse = new Button { Content = "参照...", Width = 70, Margin = new Thickness(6, 4, 0, 8) };
            browse.Click += BrowseButton_Click;
            DockPanel.SetDock(browse, Dock.Right);
            directoryPanel.Children.Add(browse);
            directoryPanel.Children.Add(_directoryTextBox);
            root.Children.Add(directoryPanel);
            root.Children.Add(new TextBlock { Text = "ファイル名の接頭辞" });
            root.Children.Add(_prefixTextBox);
            root.Children.Add(new TextBlock { Text = "出力対象" });
            var scopePanel = new StackPanel { Orientation = Orientation.Horizontal };
            scopePanel.Children.Add(_currentRadioButton);
            scopePanel.Children.Add(_allRadioButton);
            root.Children.Add(scopePanel);
            root.Children.Add(new TextBlock { Text = "画像形式" });
            root.Children.Add(_formatComboBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "出力", IsDefault = true, Width = 80, Margin = new Thickness(4) };
            var cancel = new Button { Content = "キャンセル", IsCancel = true, Width = 80, Margin = new Thickness(4) };
            ok.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_directoryTextBox.Text))
                {
                    MessageBox.Show("出力先フォルダーを指定してください。", "画像一括出力", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                DialogResult = true;
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            root.Children.Add(buttons);
            Content = root;
        }

        public string OutputDirectory => _directoryTextBox.Text;
        public string FilePrefix => _prefixTextBox.Text;
        public PageExportScope Scope => _allRadioButton.IsChecked == true ? PageExportScope.AllPages : PageExportScope.CurrentPage;
        public PageExportFormat Format => _formatComboBox.SelectedIndex == 1 ? PageExportFormat.Jpeg : PageExportFormat.Png;

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                InitialDirectory = Directory.Exists(_directoryTextBox.Text) ? _directoryTextBox.Text : Environment.CurrentDirectory,
                Filter = "画像ファイル|*.png;*.jpg",
                FileName = _prefixTextBox.Text + ".png",
                OverwritePrompt = false,
            };
            if (dialog.ShowDialog(this) != true) return;
            _directoryTextBox.Text = Path.GetDirectoryName(dialog.FileName) ?? _directoryTextBox.Text;
            _prefixTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }
}
