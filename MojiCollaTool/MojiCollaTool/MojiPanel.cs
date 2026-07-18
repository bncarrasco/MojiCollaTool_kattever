using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace MojiCollaTool
{
    public class MojiPanel : ContentControl, IDisposable
    {
        /// <summary>
        /// 文字のID
        /// </summary>
        public int Id => MojiData.Id;

        /// <summary>
        /// 代表文字列
        /// </summary>
        public string ExampleText => MojiData.ExampleText;

        public MojiData MojiData { get; set; }

        public MojiWindow? MojiWindow { get; set; }

        public PageEditorControl PageEditor => pageEditor;

        public IReadOnlyList<AttachedSymbolVisual> AttachedSymbolVisuals => attachedSymbolVisuals;

        public IReadOnlyDictionary<int, DecoratedCharacterControl> GraphemeVisuals => graphemeControls;

        public IEnumerable<AttachedSymbolData> AttachedSymbols => attachedSymbolVisuals.Select(item => item.SymbolData);

        /// <summary>
        /// 常に前面に表示するかどうかのフラグ
        /// </summary>
        public bool ShowTopmost { get; set; } = false;

        /// <summary>
        /// 背景配置用のグリッド
        /// </summary>
        private Grid backgroundGrid = new Grid();
        private Canvas attachedSymbolCanvas = new Canvas();

        /// <summary>
        /// 文字列を配置するパネル
        /// </summary>
        private StackPanel stackPanel = new StackPanel();

        /// <summary>
        /// 背景ボックス
        /// </summary>
        private Rectangle backgroundBoxRectangle = new Rectangle();

        /// <summary>
        /// 文字オブジェクトを再利用のためのオブジェクトプール
        /// </summary>
        private DecoratedCharacterControlTotalPool decoratedCharacterControlTotalPool = new DecoratedCharacterControlTotalPool();
        private readonly Dictionary<int, DecoratedCharacterControl> graphemeControls = new();
        private readonly List<AttachedSymbolVisual> attachedSymbolVisuals = new();

        /// <summary>
        /// 前回のパネルの幅
        /// 縦書きで開業が起きた際に、元の場所に戻すために使用する
        /// </summary>
        private double previousWidth;

        private PageEditorControl pageEditor;

        private Nullable<Point> dragStart = null;
        private bool dragMoved;

        /// <summary>
        /// パネルがダブルクリックされたことを示すフラグ
        /// </summary>
        private bool panelDoubleClicked = false;

        private bool _isDisposed;

        public MojiPanel(int id, PageEditorControl pageEditor)
        {
            this.pageEditor = pageEditor;

            MojiData = new MojiData(id);

            Init();
        }

        public MojiPanel(MojiData mojiData, PageEditorControl pageEditor)
        {
            this.pageEditor = pageEditor;

            MojiData = mojiData;

            Init();
        }

        private void Init()
        {
            CreateMojiWindow();

            AddChild(backgroundGrid);

            //  透明色を設定しておく
            //  これによりマウスのヒットボックスが背景にも及ぶようになる
            backgroundGrid.Background = Brushes.Transparent;
            backgroundGrid.Children.Add(backgroundBoxRectangle);

            //  透明色を設定しておく
            //  これによりマウスのヒットボックスが背景にも及ぶようになる
            stackPanel.Background = Brushes.Transparent;
            stackPanel.VerticalAlignment = VerticalAlignment.Center;
            stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
            backgroundGrid.Children.Add(stackPanel);
            attachedSymbolCanvas.IsHitTestVisible = true;
            backgroundGrid.Children.Add(attachedSymbolCanvas);

            MouseDown += MojiPanel_MouseDown;
            MouseUp += MojiPanel_MouseUp;
            MouseMove += MojiPanel_MouseMove;
            MouseDoubleClick += MojiPanel_MouseDoubleClick;
            Unloaded += MojiPanel_Unloaded;

            UpdateMojiView(true);
        }

        /// <summary>
        /// 文字を複製する
        /// </summary>
        public void Reproduction()
        {
            pageEditor.ReproductionMoji(this);
        }

        /// <summary>
        /// 文字を削除する
        /// </summary>
        public void Remove()
        {
            pageEditor.RemoveMojiPanel(this);
        }

        private void MojiPanel_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //  フォーカスを設定する処理は別の処理で奪われるため、フラグだけ立てて後で処理させる
            panelDoubleClicked = true;
        }

        private void MojiPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragStart != null && e.LeftButton == MouseButtonState.Pressed)
            {
                var element = (UIElement)sender;
                var p2 = e.GetPosition(pageEditor.Canvas);

                MojiData.X = p2.X - dragStart.Value.X;
                MojiData.Y = p2.Y - dragStart.Value.Y;
                dragMoved = true;

                Margin = new Thickness(MojiData.X, MojiData.Y, 0, 0);
            }
        }

        private void MojiPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var element = (UIElement)sender;

            if(dragStart != null)
            {
                MojiData.X = VisualOffset.X;
                MojiData.Y = VisualOffset.Y;
                MojiWindow?.UpdateXY(MojiData.X, MojiData.Y);
                if (dragMoved) pageEditor.NotifyContentChanged("位置変更", MojiData.ObjectId.ToString("D"));
            }
            dragStart = null;
            dragMoved = false;
            element.ReleaseMouseCapture();

            //  パネルがダブルクリックされた場合の後処理
            //  文字画面へのフォーカスを設定する処理を行う
            if(panelDoubleClicked)
            {
                ShowMojiWindow();

                panelDoubleClicked = false;
            }
        }

        /// <summary>
        /// 文字画面を表示する
        /// </summary>
        public void ShowMojiWindow()
        {
            if (_isDisposed) return;

            CreateMojiWindow();
            MojiWindow!.Topmost = ShowTopmost;
            MojiWindow.Show();
            MojiWindow.Activate();
        }

        private void CreateMojiWindow()
        {
            if (_isDisposed || MojiWindow != null) return;

            MojiWindow = new MojiWindow(this);
            MojiWindow.Closed += MojiWindow_Closed;
        }

        private void MojiWindow_Closed(object? sender, EventArgs e)
        {
            var window = MojiWindow;
            if (window == null || !ReferenceEquals(sender, window)) return;

            window.Closed -= MojiWindow_Closed;
            MojiWindow = null;
        }

        private void MojiPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = (UIElement)sender;
            dragStart = e.GetPosition(element);
            dragMoved = false;
            element.CaptureMouse();
        }

        internal void NotifyContentChanged(string description = "ページ編集", string? coalesceKey = null)
            => pageEditor.NotifyContentChanged(description, coalesceKey);

        private void MojiPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            // 一時的なVisualTreeからの離脱では編集Windowを破棄しない。
            // 永続的な削除・ページ破棄はDispose()から明示的に行う。
        }

        /// <summary>
        /// 文字パネルを最終破棄し、所有する編集Windowも終了させます。
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            RemoveAllAttachedSymbols();

            var window = MojiWindow;
            if (window == null) return;

            window.Closed -= MojiWindow_Closed;
            MojiWindow = null;
            window.IsHideOnly = false;
            window.Close();
        }

        /// <summary>
        /// 文字パネルの位置を更新する
        /// </summary>
        public void UpdateXYView()
        {
            //  文字パネルの位置を設定する
            Margin = new Thickness(MojiData.X, MojiData.Y, 0, 0);
        }

        /// <summary>
        /// 文字の表示を更新する
        /// </summary>
        public void UpdateMojiView(bool isTextDecorationUpdated)
        {
            //  文字パネルの中身をクリアする
            foreach (StackPanel child in stackPanel.Children)
            {
                child.Children.Clear();
            }

            //  文字の装飾が更新された場合、プールされている文字は再利用できない
            //  文字のプールをクリ化する
            if(isTextDecorationUpdated)
            {
                decoratedCharacterControlTotalPool.Clear();
            }

            //  文字オブジェクトプールの使用状況をリセットする
            decoratedCharacterControlTotalPool.ResetUsedCounter();
            graphemeControls.Clear();

            //  文字パネルの位置を設定する
            Margin = new Thickness(MojiData.X, MojiData.Y, 0, 0);

            //  縦書き、横書きで配置方法が異なる
            switch (MojiData.TextDirection)
            {
                case TextDirection.Yokogaki:
                    stackPanel.Orientation = Orientation.Vertical;
                    break;
                case TextDirection.Tategaki:
                    stackPanel.Orientation = Orientation.Horizontal;
                    break;
                default:
                    break;
            }

            // 改行と本文は書記素単位で分ける。char列にするとサロゲート
            // pair、結合文字、ZWJ sequenceが分割されてしまう。
            var lines = new List<List<GraphemeCluster>> { new List<GraphemeCluster>() };
            foreach (var grapheme in GraphemeService.Segment(MojiData.FullText))
            {
                if (grapheme.Text == "\r\n" || grapheme.Text == "\r" || grapheme.Text == "\n")
                {
                    lines.Add(new List<GraphemeCluster>());
                }
                else
                {
                    lines.Last().Add(grapheme);
                }
            }

            List<Panel> linePanels = new List<Panel>();

            foreach (var line in lines)
            {
                //  行ごとにスタックパネルを用意する
                var linePanel = new StackPanel();

                //  配置方向を設定する
                //  行間を設定する
                //  縦書き横書きで異なる
                switch (MojiData.TextDirection)
                {
                    case TextDirection.Yokogaki:
                        linePanel.Margin = new Thickness(0, 0, 0, MojiData.LineMargin);
                        linePanel.Orientation = Orientation.Horizontal;
                        break;
                    case TextDirection.Tategaki:
                        linePanel.Margin = new Thickness(0, 0, MojiData.LineMargin, 0);
                        linePanel.Orientation = Orientation.Vertical;
                        break;
                    default:
                        break;
                }

                //  空の行だった場合、配置されずにずれるため、全角スペースを入れておく
                if (line.Count <= 0)
                {
                    var placeholder = decoratedCharacterControlTotalPool.GetDecoratedCharacterControl('　', MojiData);
                    linePanel.Children.Add(placeholder);
                }
                else foreach (var grapheme in line)
                {
                    //  縦書きのために、１文字ずつ文字を作成する
                    var decoratedCharacterControl = decoratedCharacterControlTotalPool.GetDecoratedCharacterControl(grapheme.Text, MojiData);
                    decoratedCharacterControl.GraphemeIndex = grapheme.Index;
                    graphemeControls[grapheme.Index] = decoratedCharacterControl;

                    //  行パネルに追加する
                    linePanel.Children.Add(decoratedCharacterControl);
                }

                linePanels.Add(linePanel);
            }

            //  縦書きの場合、逆順に配置する必要あり
            if (MojiData.TextDirection == TextDirection.Tategaki)
            {
                linePanels.Reverse();

                //  縦書きの末尾の行の要素のマージンは不要、あった場合、マイナス値の場合文字が隠れていく
                linePanels.Last().Margin = new Thickness(0);
            }

            foreach (var linePanel in linePanels)
            {
                stackPanel.Children.Add(linePanel);
            }

            //  背景ボックスを設定する
            if(MojiData.IsBackgroundBoxExists)
            {
                //  背景ボックスのパディング設定
                stackPanel.Margin = new Thickness(MojiData.BackgroundBoxPadding);

                //  背景ボックスの色、縁取りなどの設定
                backgroundBoxRectangle.Fill = new SolidColorBrush(MojiData.BackgroundBoxColor);
                backgroundBoxRectangle.Stroke = new SolidColorBrush(MojiData.BackgroundBoxBorderColor);
                backgroundBoxRectangle.StrokeThickness = MojiData.BackgroundBoxBorderThickness;
                backgroundBoxRectangle.RadiusX = backgroundBoxRectangle.RadiusY = MojiData.BackgroundBoxCornerRadius;
            }
            else
            {
                //  背景ボックスのパディングをゼロにする
                stackPanel.Margin = new Thickness(0);

                //  背景ボックスの色、縁取りなどを消す
                backgroundBoxRectangle.Fill = Brushes.Transparent;
                backgroundBoxRectangle.StrokeThickness = 0;
                backgroundBoxRectangle.Stroke = Brushes.Transparent;
                backgroundBoxRectangle.RadiusX = backgroundBoxRectangle.RadiusY = 0;
            }

            //  文字の回転を行う
            if (MojiData.IsRotateActive)
            {
                backgroundGrid.RenderTransformOrigin = new Point(0.5, 0.5);
                backgroundGrid.RenderTransform = new RotateTransform(MojiData.RotateAngle);
            }
            else
            {
                backgroundGrid.RenderTransformOrigin = new Point(0, 0);
                backgroundGrid.RenderTransform = null;
            }
            
            //  レイアウトを計算し直すために呼んでいる
           UpdateLayout();

            if(MojiData.TextDirection == TextDirection.Tategaki)
            {
                //  縦書きで前回より位置がずれた場合、X座標を元の位置に戻す
                if(previousWidth > 0)
                {
                    Margin = new Thickness(Math.Round(Margin.Left - (backgroundGrid.DesiredSize.Width - previousWidth)), Margin.Top, Margin.Right, Margin.Bottom);
                    MojiData.X = Margin.Left;
                    MojiWindow?.UpdateXY(MojiData.X, MojiData.Y);
                }

                //  この時点でのパネルの幅を保存しておく
                //  縦書きで元の位置に戻すため
                previousWidth = backgroundGrid.DesiredSize.Width;
            }
            else
            {
                //  横書きの際は使用しないため、0に戻しておく
                //  縦横切り替えでウォーキングが際限なくずれるのが面倒なため
                previousWidth = 0;
            }

            RefreshAttachedSymbols();
        }

        public AttachedSymbolVisual AddAttachedSymbol(AttachedSymbolData symbol)
        {
            if (symbol == null) throw new ArgumentNullException(nameof(symbol));
            var visual = new AttachedSymbolVisual(symbol, MojiData);
            attachedSymbolVisuals.Add(visual);
            attachedSymbolCanvas.Children.Add(visual);
            RefreshAttachedSymbols();
            return visual;
        }

        public bool RemoveAttachedSymbol(Guid symbolId)
        {
            var visual = attachedSymbolVisuals.FirstOrDefault(item => item.ObjectId == symbolId);
            if (visual == null) return false;
            attachedSymbolVisuals.Remove(visual);
            attachedSymbolCanvas.Children.Remove(visual);
            return true;
        }

        public void RemoveAllAttachedSymbols()
        {
            foreach (var visual in attachedSymbolVisuals.ToArray())
                attachedSymbolCanvas.Children.Remove(visual);
            attachedSymbolVisuals.Clear();
        }

        public Rect GetGraphemeAnchorBounds(int graphemeIndex)
        {
            if (!graphemeControls.TryGetValue(graphemeIndex, out var control)) return Rect.Empty;
            var size = new Size(Math.Max(1, control.ActualWidth > 0 ? control.ActualWidth : control.Width),
                Math.Max(1, control.ActualHeight > 0 ? control.ActualHeight : control.Height));
            return control.TransformToVisual(backgroundGrid).TransformBounds(new Rect(new Point(0, 0), size));
        }

        private void RefreshAttachedSymbols()
        {
            foreach (var visual in attachedSymbolVisuals)
            {
                if (visual.SymbolData.IsDetached || !visual.SymbolData.ParentId.HasValue ||
                    visual.SymbolData.ParentId.Value != MojiData.ObjectId)
                {
                    visual.Visibility = Visibility.Hidden;
                    visual.IsHitTestVisible = false;
                    continue;
                }

                var bounds = GetGraphemeAnchorBounds(visual.SymbolData.GraphemeAnchor);
                visual.Visibility = bounds.IsEmpty ? Visibility.Hidden : Visibility.Visible;
                visual.ApplyData(visual.SymbolData, MojiData, bounds.IsEmpty ? new Rect(0, 0, 0, 0) : bounds);
            }
        }
    }
}
