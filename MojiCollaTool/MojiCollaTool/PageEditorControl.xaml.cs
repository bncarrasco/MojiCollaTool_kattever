using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MojiCollaTool
{
    /// <summary>
    /// 1ページ分のキャンバス、画像、文字オブジェクト、ページ内操作を保持するコントロールです。
    /// MainWindowはプロジェクト入出力などのアプリケーションシェルに集中します。
    /// </summary>
    public partial class PageEditorControl : UserControl, IDisposable
    {
        private readonly List<MojiPanel> _mojiPanels = new();
        private readonly ObservableCollection<MojiPanel> _viewMojiPanels = new();
        private CanvasEditWindow? _canvasEditWindow;
        private bool _runEvent;
        private bool _isDisposed;

        public PageEditorControl()
        {
            InitializeComponent();
            MojiListView.ItemsSource = _viewMojiPanels;
            ResetScale();
            _runEvent = true;
        }

        public CanvasData CanvasData { get; private set; } = new();

        public Canvas Canvas => MainCanvas;

        public IReadOnlyList<MojiPanel> MojiPanels => _mojiPanels;

        public IEnumerable<MojiData> MojiDatas => _mojiPanels.Select(panel => panel.MojiData);

        public bool HasMojiPanels => _mojiPanels.Count > 0;

        public event EventHandler<PageFileDropEventArgs>? FileDropped;

        public void RefreshMojiList()
        {
            _viewMojiPanels.Clear();
            foreach (var mojiPanel in _mojiPanels)
            {
                _viewMojiPanels.Add(mojiPanel);
            }
        }

        public void SetCanvasData(CanvasData canvasData)
        {
            ThrowIfDisposed();
            CanvasData = canvasData ?? throw new ArgumentNullException(nameof(canvasData));
            UpdateCanvas();
        }

        public void AddNewMojiPanel() => AddMojiPanel(new MojiPanel(GetNextMojiId(), this));

        public void AddMojiPanel(MojiPanel mojiPanel)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(mojiPanel);
            if (_mojiPanels.Contains(mojiPanel)) return;

            _mojiPanels.Add(mojiPanel);
            _viewMojiPanels.Add(mojiPanel);
            MainCanvas.Children.Add(mojiPanel);
        }

        public void ReproductionMoji(MojiPanel mojiPanel)
        {
            ArgumentNullException.ThrowIfNull(mojiPanel);
            AddMojiPanel(new MojiPanel(mojiPanel.MojiData.Reproduct(GetNextMojiId()), this));
        }

        public void RemoveMojiPanel(MojiPanel mojiPanel)
        {
            if (!_mojiPanels.Remove(mojiPanel)) return;

            _viewMojiPanels.Remove(mojiPanel);
            mojiPanel.Dispose();
            MainCanvas.Children.Remove(mojiPanel);
        }

        public void RemoveAllMojiPanel()
        {
            foreach (var panel in _mojiPanels.ToArray()) RemoveMojiPanel(panel);
        }

        public void LoadImage(string filePath, int imageNumber)
        {
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));

            var imageSource = ImageUtil.LoadImageSource2(filePath);
            var image = imageNumber == 1 ? ImageControl1 : ImageControl2;
            image.Source = imageSource;
            var imageData = new ImageData((int)imageSource.Width, (int)imageSource.Height);

            if (imageNumber == 1) CanvasData.ImageData1 = imageData;
            else CanvasData.ImageData2 = imageData;
        }

        public void UnloadImage(int imageNumber)
        {
            if (imageNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(imageNumber));
            (imageNumber == 1 ? ImageControl1 : ImageControl2).Source = null;
        }

        public void CloseCanvasEditor()
        {
            var window = _canvasEditWindow;
            _canvasEditWindow = null;
            window?.Close();
        }

        /// <summary>
        /// ページの最終破棄時に、ページが所有するWindowと文字パネルを解放します。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            CloseCanvasEditor();
            foreach (var panel in _mojiPanels.ToArray()) panel.Dispose();
            _mojiPanels.Clear();
            _viewMojiPanels.Clear();
            MainCanvas.Children.Clear();
            FileDropped = null;
        }

        internal void OnCanvasEditWindowClosed(CanvasEditWindow window)
        {
            if (ReferenceEquals(_canvasEditWindow, window)) _canvasEditWindow = null;
        }

        public void ExportImage(string filePath, BitmapEncoder encoder)
        {
            var previousScale = ScalingTextBox.Value;
            try
            {
                UpdateScale(100);
                MainCanvas.ToImage(filePath, encoder);
            }
            finally
            {
                UpdateScale(previousScale);
            }
        }

        public void UpdateCanvas()
        {
            if (!CanvasData.ImageData1.IsNullData())
            {
                ImageControl1.Width = CanvasData.ImageData1.ModifiedWidth;
                ImageControl1.Height = CanvasData.ImageData1.ModifiedHeight;
            }

            if (!CanvasData.ImageData2.IsNullData())
            {
                ImageControl2.Width = CanvasData.ImageData2.ModifiedWidth;
                ImageControl2.Height = CanvasData.ImageData2.ModifiedHeight;
            }

            MainCanvas.Width = CanvasData.CanvasWidth;
            MainCanvas.Height = CanvasData.CanvasHeight;
            CanvasBackgroundRect.Fill = new SolidColorBrush(CanvasData.CanvasColor);
            CanvasBackgroundRect.Width = CanvasData.CanvasWidth;
            CanvasBackgroundRect.Height = CanvasData.CanvasHeight;
            ImageControl1.Margin = CanvasData.GetImage1Margin();
            ImageControl2.Margin = CanvasData.GetImage2Margin();
        }

        private int GetNextMojiId() => _mojiPanels.Count == 0 ? 1 : _mojiPanels.Max(x => x.Id) + 1;

        private void ResetScale()
        {
            _runEvent = false;
            ScalingTextBox.SetValue(100, false);
            UpdateScale(100);
            _runEvent = true;
        }

        private void UpdateScale(int scalePercent)
        {
            CanvasScaleTransform.ScaleX = scalePercent / 100.0;
            CanvasScaleTransform.ScaleY = scalePercent / 100.0;
            if (ScalingTextBox.Value != scalePercent) ScalingTextBox.SetValue(scalePercent, false);
        }

        private void ScalingTextBox_ValueChanged(object sender, UpDownTextBoxEvent e)
        {
            if (_runEvent) UpdateScale(e.Value);
        }

        private void CanvasGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) ScalingTextBox.RunUpButton();
            else if (e.Delta < 0) ScalingTextBox.RunDownButton();
        }

        private void MojiListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MojiListView.SelectedItem is MojiPanel mojiPanel) mojiPanel.ShowMojiWindow();
        }

        private void CanvasEditButton_Click(object sender, RoutedEventArgs e)
        {
            ThrowIfDisposed();
            if (_canvasEditWindow == null || !_canvasEditWindow.IsVisible)
            {
                _canvasEditWindow = new CanvasEditWindow(CanvasData, this);
                _canvasEditWindow.Show();
            }
        }

        private void CanvasGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (filePaths.Length == 0) return;

            FileDropped?.Invoke(this, new PageFileDropEventArgs(filePaths[0]));
            e.Handled = true;
        }

        private void CanvasGrid_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var extension = filePaths.Length == 0 ? string.Empty : System.IO.Path.GetExtension(filePaths[0]);
            e.Effects = extension is ".jpg" or ".png" or ".mctzip"
                ? DragDropEffects.All
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(PageEditorControl));
        }
    }

    public sealed class PageFileDropEventArgs : EventArgs
    {
        public PageFileDropEventArgs(string filePath) => FilePath = filePath;

        public string FilePath { get; }
    }
}
