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

        /// <summary>
        /// 常に前面に表示するかどうかのフラグ
        /// </summary>
        public bool ShowTopmost { get; set; } = false;

        /// <summary>
        /// 背景配置用のグリッド
        /// </summary>
        private Grid backgroundGrid = new Grid();

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

        /// <summary>
        /// 前回のパネルの幅
        /// 縦書きで開業が起きた際に、元の場所に戻すために使用する
        /// </summary>
        private double previousWidth;

        private PageEditorControl pageEditor;

        private Nullable<Point> dragStart = null;

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
            }
            dragStart = null;
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
            element.CaptureMouse();
        }

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

            //  改行ごとに分ける
            var lines = MojiData.GetTextLines();

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

                var characters = new List<Char>(line);

                //  空の行だった場合、配置されずにずれるため、全角スペースを入れておく
                if (characters.Count <= 0)
                {
                    characters.Add('　');
                }

                foreach (var character in characters)
                {
                    //  縦書きのために、１文字ずつ文字を作成する
                    DecoratedCharacterControl decoratedCharacterControl = decoratedCharacterControlTotalPool.GetDecoratedCharacterControl(character, MojiData);

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
        }
    }
}
