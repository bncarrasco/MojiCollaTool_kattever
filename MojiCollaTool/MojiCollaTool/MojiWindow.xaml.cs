using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MojiCollaTool
{
    /// <summary>
    /// MojiWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MojiWindow : Window
    {
        private MojiPanel _mojiPanel;

        /// <summary>
        /// 画面を隠すことのみを示すフラグ
        /// 画面の本当の破棄処理を制御するために用意している
        /// </summary>
        public bool IsHideOnly { get; set; } = true;

        private bool _runEvent = false;
        private bool _updatingAttachedSymbolUi;

        public MojiWindow(MojiPanel mojiPanel)
        {
            this._mojiPanel= mojiPanel;

            InitializeComponent();

            FontFamilyComboBox.ItemsSource = FontUtil.GetFontTextBlocks();
            AttachedSymbolFontFamilyComboBox.ItemsSource = FontUtil.GetFontTextBlocks();

            ShowTopMostCheckBox.IsChecked = mojiPanel.ShowTopmost;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            LoadMojiDataToWindow(_mojiPanel.MojiData);

            _runEvent = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //  画面が閉じられても、隠すだけにしている
            //  ただしこの場合だとアプリが終わってもウィンドウが保持されたままとなりアプリが終了しない
            //  隠すのをやめるフラグを設定することで終了可能にする
            if(IsHideOnly)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void LoadMojiDataToWindow(MojiData mojiData)
        {
            _runEvent = false;

            Title = $"[{mojiData.Id}] {mojiData.ExampleText}";
            IDLabel.Content = $"Moji ID:{mojiData.Id}";

            TextTextBox.Text = mojiData.FullText;

            LocationXTextBox.SetValue((int)mojiData.X, false);
            LocationYTextBox.SetValue((int)mojiData.Y, false);

            DirectionComboBox.SelectedIndex = (int)mojiData.TextDirection;
            RotateTextBox.SetValue((int)mojiData.RotateAngle);

            FontSizeTextBox.SetValue(mojiData.FontSize, false);
            if (FontUtil.GetFontFamilies().ContainsKey(mojiData.FontFamilyName))
            {
                FontFamilyComboBox.SelectedValue = mojiData.FontFamilyName;
            }
            BoldCheckBox.IsChecked = mojiData.IsBold;
            ItalicCheckBox.IsChecked = mojiData.IsItalic;

            CharacterMarginTextBox.SetValue((int)mojiData.CharacterMargin);
            LineMarginTextBox.SetValue((int)mojiData.LineMargin);

            ForeColorButton.Background = new SolidColorBrush(mojiData.ForeColor);

            BorderThicknessTextBox.SetValue((int)mojiData.BorderThickness);
            BorderColorButton.Background = new SolidColorBrush(mojiData.BorderColor);
            BorderBlurrRadiusTextBox.SetValue((int)mojiData.BorderBlurrRadius);

            SecondBorderThicknessTextBox.SetValue((int)mojiData.SecondBorderThickness);
            SecondBorderColorButton.Background = new SolidColorBrush(mojiData.SecondBorderColor);
            SecondBorderBlurrRadiusTextBox.SetValue((int)mojiData.SecondBorderBlurrRadius);

            BackgroundBoxCheckBox.IsChecked = mojiData.IsBackgroundBoxExists;
            BackgroundBoxColorButton.Background = new SolidColorBrush(mojiData.BackgroundBoxColor);
            BackgroundBoxPaddingTextBox.SetValue((int)mojiData.BackgroundBoxPadding);

            BackgroundBoxPaddingCornerRadiusTextBox.SetValue((int)mojiData.BackgroundBoxCornerRadius);

            BackgroundBoxBorderColorButton.Background = new SolidColorBrush(mojiData.BackgroundBoxBorderColor);
            BackgroundBoxBorderThicknessTextBox.SetValue((int)mojiData.BackgroundBoxBorderThickness);

            LoadAttachedSymbolsToWindow();

            _runEvent = true;
        }

        private AttachedSymbolVisual? SelectedAttachedSymbol => AttachedSymbolListBox.SelectedItem as AttachedSymbolVisual;

        private void LoadAttachedSymbolsToWindow(AttachedSymbolVisual? preferred = null)
        {
            _updatingAttachedSymbolUi = true;
            try
            {
                var graphemes = _mojiPanel.MojiData.Graphemes
                    .Select(item => new GraphemeChoice(item.Index, item.Text))
                    .ToArray();
                AttachedSymbolGraphemeComboBox.ItemsSource = graphemes;
                AttachedSymbolListBox.ItemsSource = _mojiPanel.AttachedSymbolVisuals.ToArray();
                var selectedId = preferred?.ObjectId ?? _mojiPanel.PageEditor.SelectedAttachedSymbolId;
                var selected = selectedId.HasValue
                    ? _mojiPanel.AttachedSymbolVisuals.FirstOrDefault(item => item.ObjectId == selectedId.Value)
                    : null;
                AttachedSymbolListBox.SelectedItem = selected;
                if (selected == null)
                {
                    if (graphemes.Length > 0) AttachedSymbolGraphemeComboBox.SelectedIndex = 0;
                    AttachedSymbolTextBox.Text = "!";
                    AttachedSymbolSelectedTextBlock.Text = string.Empty;
                    AttachedSymbolFontStatusTextBlock.Text = string.Empty;
                    return;
                }

                AttachedSymbolGraphemeComboBox.SelectedValue = selected.SymbolData.GraphemeAnchor;
                AttachedSymbolSelectedTextBlock.Text = selected.SymbolData.Text;
                AttachedSymbolOffsetXTextBox.Text = selected.SymbolData.OffsetX.ToString(CultureInfo.InvariantCulture);
                AttachedSymbolOffsetYTextBox.Text = selected.SymbolData.OffsetY.ToString(CultureInfo.InvariantCulture);
                AttachedSymbolScaleTextBox.Text = selected.SymbolData.Scale.ToString(CultureInfo.InvariantCulture);
                AttachedSymbolRotationTextBox.Text = selected.SymbolData.Rotation.ToString(CultureInfo.InvariantCulture);
                AttachedSymbolFontSizeTextBox.Text = selected.SymbolData.FontSize.ToString(CultureInfo.InvariantCulture);
                AttachedSymbolForeColorTextBox.Text = $"#{selected.SymbolData.ForeColorArgb:X8}";
                AttachedSymbolInheritFontCheckBox.IsChecked = selected.SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Font);
                AttachedSymbolInheritColorCheckBox.IsChecked = selected.SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.ForeColor);
                AttachedSymbolInheritBorderCheckBox.IsChecked = selected.SymbolData.Inherit.HasFlag(AttachedSymbolInheritance.Border);
                AttachedSymbolFontFamilyComboBox.SelectedValue = selected.SymbolData.FontFamilyName;
                AttachedSymbolFontStatusTextBlock.Text = selected.FontStatus;
            }
            finally
            {
                _updatingAttachedSymbolUi = false;
            }
        }

        public void UpdateXY(double x, double y)
        {
            _runEvent = false;

            LocationXTextBox.SetValue((int)x, false);
            LocationYTextBox.SetValue((int)y, false);

            _runEvent = true;
        }

        private void ReproductionButton_Click(object sender, RoutedEventArgs e)
        {
            _mojiPanel.Reproduction();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.ShowOKCancelDialog("文字を削除してよろしいですか？") == false) return;

            _mojiPanel.Remove();
        }

        public void UpdateMojiView(bool isTextDecoraitonUpdated, string changeDescription = "スタイル変更", string? coalesceKey = null)
        {
            _mojiPanel.MojiData.FullText = TextTextBox.Text;
            Title = $"[{_mojiPanel.MojiData.Id}] {_mojiPanel.MojiData.ExampleText}";
            _mojiPanel.MojiData.FontSize = FontSizeTextBox.Value;
            _mojiPanel.MojiData.X = LocationXTextBox.Value;
            _mojiPanel.MojiData.Y = LocationYTextBox.Value;
            _mojiPanel.MojiData.TextDirection = (TextDirection)DirectionComboBox.SelectedIndex;
            _mojiPanel.MojiData.IsBold = (BoldCheckBox.IsChecked == true);
            _mojiPanel.MojiData.IsItalic = (ItalicCheckBox.IsChecked == true);
            _mojiPanel.MojiData.LineMargin = LineMarginTextBox.Value;
            _mojiPanel.MojiData.CharacterMargin = CharacterMarginTextBox.Value;
            _mojiPanel.MojiData.FontFamilyName = (string)FontFamilyComboBox.SelectedValue;
            _mojiPanel.MojiData.BorderThickness = BorderThicknessTextBox.Value;
            _mojiPanel.MojiData.BorderBlurrRadius = BorderBlurrRadiusTextBox.Value;
            _mojiPanel.MojiData.SecondBorderThickness = SecondBorderThicknessTextBox.Value;
            _mojiPanel.MojiData.SecondBorderBlurrRadius = SecondBorderBlurrRadiusTextBox.Value;
            _mojiPanel.MojiData.IsBackgroundBoxExists = (BackgroundBoxCheckBox.IsChecked == true);
            _mojiPanel.MojiData.BackgroundBoxPadding = BackgroundBoxPaddingTextBox.Value;
            _mojiPanel.MojiData.BackgroundBoxBorderThickness = BackgroundBoxBorderThicknessTextBox.Value;
            _mojiPanel.MojiData.BackgroundBoxCornerRadius = BackgroundBoxPaddingCornerRadiusTextBox.Value;
            _mojiPanel.MojiData.RotateAngle = RotateTextBox.Value;

            _mojiPanel.UpdateMojiView(isTextDecoraitonUpdated);
            if (_runEvent) _mojiPanel.NotifyContentChanged(changeDescription, coalesceKey);
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_runEvent == false) return;

            UpdateMojiView(false, "文字入力", _mojiPanel.MojiData.ObjectId.ToString("D"));
        }

        private void TextBox_ValueChanged(object sender, UpDownTextBoxEvent e)
        {
            if (_runEvent == false) return;

            UpdateMojiView(true, "スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_runEvent == false) return;

            UpdateMojiView(true, "スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
        }

        private void CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (_runEvent == false) return;

            UpdateMojiView(true, "スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
        }

        private void ColorButton_Click(Color currentColor, Action<Color> action)
        {
            if (_runEvent == false) return;

            ColorSelector.ColorSelectorWindow colorSelectorWindow = new ColorSelector.ColorSelectorWindow(currentColor, action);
            colorSelectorWindow.Top = Top;
            colorSelectorWindow.Left = Left;
            colorSelectorWindow.Topmost = Topmost;
            var dialogResult = colorSelectorWindow.ShowDialog();

            if (dialogResult.HasValue == false || dialogResult.Value == false)
            {
                //  色を元に戻す
                action(currentColor);
            }
        }

        private void ForeColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorButton_Click(_mojiPanel.MojiData.ForeColor, (color) =>
            {
                _mojiPanel.MojiData.ForeColor = color;
                ((Button)sender).Background = new SolidColorBrush(color);
                _mojiPanel.UpdateMojiView(true);
                _mojiPanel.NotifyContentChanged("スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
            });
        }

        private void BorderColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorButton_Click(_mojiPanel.MojiData.BorderColor, (color) =>
            {
                _mojiPanel.MojiData.BorderColor = color;
                ((Button)sender).Background = new SolidColorBrush(color);
                _mojiPanel.UpdateMojiView(true);
                _mojiPanel.NotifyContentChanged("スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
            });
        }

        private void SecondBorderColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorButton_Click(_mojiPanel.MojiData.SecondBorderColor, (color) =>
            {
                _mojiPanel.MojiData.SecondBorderColor = color;
                ((Button)sender).Background = new SolidColorBrush(color);
                _mojiPanel.UpdateMojiView(true);
                _mojiPanel.NotifyContentChanged("スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
            });
        }

        private void BackgroundBoxColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorButton_Click(_mojiPanel.MojiData.BackgroundBoxColor, (color) =>
            {
                _mojiPanel.MojiData.BackgroundBoxColor = color;
                ((Button)sender).Background = new SolidColorBrush(color);
                _mojiPanel.UpdateMojiView(true);
                _mojiPanel.NotifyContentChanged("スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
            });
        }

        private void BackgroundBoxBorderColorButton_Click(object sender, RoutedEventArgs e)
        {
            ColorButton_Click(_mojiPanel.MojiData.BackgroundBoxBorderColor, (color) =>
            {
                _mojiPanel.MojiData.BackgroundBoxBorderColor = color;
                ((Button)sender).Background = new SolidColorBrush(color);
                _mojiPanel.UpdateMojiView(true);
                _mojiPanel.NotifyContentChanged("スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
            });
        }

        private void AttachedSymbolGraphemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingAttachedSymbolUi || AttachedSymbolGraphemeComboBox == null || AttachedSymbolGraphemeComboBox.SelectedValue is not int) return;
        }

        private void AttachedSymbolCandidateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingAttachedSymbolUi || AttachedSymbolCandidateComboBox == null || AttachedSymbolTextBox == null ||
                AttachedSymbolCandidateComboBox.SelectedItem is not ComboBoxItem item) return;
            AttachedSymbolTextBox.Text = item.Content?.ToString() ?? string.Empty;
        }

        private void AttachedSymbolAddButton_Click(object sender, RoutedEventArgs e)
        {
            if (AttachedSymbolGraphemeComboBox.SelectedValue is not int graphemeIndex || string.IsNullOrEmpty(AttachedSymbolTextBox.Text))
            {
                MainWindow.ShowInfoDialog("対象書記素と付加記号を指定してください。", "付加記号");
                return;
            }
            var visual = _mojiPanel.PageEditor.AddAttachedSymbol(_mojiPanel.MojiData.ObjectId, graphemeIndex, AttachedSymbolTextBox.Text);
            LoadAttachedSymbolsToWindow(visual);
        }

        private void AttachedSymbolListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingAttachedSymbolUi || AttachedSymbolListBox == null) return;
            LoadAttachedSymbolsToWindow(SelectedAttachedSymbol);
        }

        private void AttachedSymbolPropertyTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingAttachedSymbolUi || AttachedSymbolListBox == null || SelectedAttachedSymbol == null) return;
            if (!TryReadDouble(AttachedSymbolOffsetXTextBox.Text, out var offsetX) ||
                !TryReadDouble(AttachedSymbolOffsetYTextBox.Text, out var offsetY) ||
                !TryReadDouble(AttachedSymbolScaleTextBox.Text, out var scale) ||
                !TryReadDouble(AttachedSymbolRotationTextBox.Text, out var rotation) || scale <= 0) return;
            _ = int.TryParse(AttachedSymbolFontSizeTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fontSize);
            if (!TryReadArgb(AttachedSymbolForeColorTextBox.Text, out var foreColorArgb)) return;
            var id = SelectedAttachedSymbol.ObjectId;
            _mojiPanel.PageEditor.UpdateAttachedSymbol(id, symbol =>
            {
                symbol.OffsetX = offsetX;
                symbol.OffsetY = offsetY;
                symbol.Scale = scale;
                symbol.Rotation = rotation;
                symbol.FontSize = Math.Max(0, fontSize);
                symbol.ForeColorArgb = foreColorArgb;
            }, "付加記号編集", id.ToString("D"));
            AttachedSymbolFontStatusTextBlock.Text = SelectedAttachedSymbol.FontStatus;
        }

        private void AttachedSymbolInheritanceChanged(object sender, RoutedEventArgs e)
        {
            if (_updatingAttachedSymbolUi || AttachedSymbolListBox == null || SelectedAttachedSymbol == null) return;
            var id = SelectedAttachedSymbol.ObjectId;
            _mojiPanel.PageEditor.UpdateAttachedSymbol(id, symbol =>
            {
                symbol.Inherit = SetFlag(symbol.Inherit, AttachedSymbolInheritance.Font, AttachedSymbolInheritFontCheckBox.IsChecked == true);
                symbol.Inherit = SetFlag(symbol.Inherit, AttachedSymbolInheritance.ForeColor, AttachedSymbolInheritColorCheckBox.IsChecked == true);
                symbol.Inherit = SetFlag(symbol.Inherit, AttachedSymbolInheritance.Border, AttachedSymbolInheritBorderCheckBox.IsChecked == true);
            }, "付加記号継承設定", id.ToString("D"));
            AttachedSymbolFontStatusTextBlock.Text = SelectedAttachedSymbol.FontStatus;
        }

        private void AttachedSymbolFontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingAttachedSymbolUi || AttachedSymbolFontFamilyComboBox == null || AttachedSymbolListBox == null || SelectedAttachedSymbol == null) return;
            if (AttachedSymbolFontFamilyComboBox.SelectedValue is not string fontName) return;
            var id = SelectedAttachedSymbol.ObjectId;
            _mojiPanel.PageEditor.UpdateAttachedSymbol(id, symbol => symbol.FontFamilyName = fontName,
                "付加記号フォント変更", id.ToString("D"));
            AttachedSymbolFontStatusTextBlock.Text = SelectedAttachedSymbol.FontStatus;
        }

        private void AttachedSymbolRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedAttachedSymbol == null) return;
            _mojiPanel.PageEditor.RemoveAttachedSymbol(SelectedAttachedSymbol.ObjectId);
            LoadAttachedSymbolsToWindow();
        }

        private static bool TryReadDouble(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

        private static bool TryReadArgb(string text, out uint value)
        {
            var normalized = text.Trim();
            if (normalized.StartsWith("#", StringComparison.Ordinal)) normalized = normalized.Substring(1);
            if (normalized.Length == 6) normalized = "FF" + normalized;
            return uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static AttachedSymbolInheritance SetFlag(AttachedSymbolInheritance value, AttachedSymbolInheritance flag, bool enabled)
            => enabled ? value | flag : value & ~flag;

        private void ShowTopMostCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (ShowTopMostCheckBox.IsChecked.HasValue == false) return;

            _mojiPanel.ShowTopmost = ShowTopMostCheckBox.IsChecked.Value;
            Topmost = _mojiPanel.ShowTopmost;
        }

        private void SaveFormatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = DataIO.GetMojiFormatDirPath();
                saveFileDialog.Filter = "moji format files|*.xml";
                var dialogResult = saveFileDialog.ShowDialog();

                if (dialogResult.HasValue == false || dialogResult.Value == false) return;

                //  保存する文字データの本文は、ファイル名と同じにする
                MojiData formatMojiData = _mojiPanel.MojiData.Clone();
                formatMojiData.FullText = System.IO.Path.GetFileNameWithoutExtension(saveFileDialog.FileName);

                DataIO.WriteMojiFormat(formatMojiData, saveFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MainWindow.ShowError("文字フォーマット保存エラー", ex);
            }
        }

        private void LoadFormatButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.InitialDirectory = DataIO.GetMojiFormatDirPath();
                openFileDialog.Filter = "moji format files|*.xml";
                var dialogResult = openFileDialog.ShowDialog();

                if (dialogResult.HasValue == false || dialogResult.Value == false) return;

                var formatMojiData = DataIO.ReadMojiData(openFileDialog.FileName);

                //  IDと座標、テキストはそのままにしておく、他はコピーする
                formatMojiData.Id = _mojiPanel.MojiData.Id;
                formatMojiData.X = _mojiPanel.MojiData.X;
                formatMojiData.Y = _mojiPanel.MojiData.Y;
                formatMojiData.FullText = _mojiPanel.MojiData.FullText;   
                _mojiPanel.MojiData.Copy(formatMojiData);

                LoadMojiDataToWindow(_mojiPanel.MojiData);

                _mojiPanel.UpdateMojiView(true);
                _mojiPanel.NotifyContentChanged("スタイル変更", _mojiPanel.MojiData.ObjectId.ToString("D"));
            }
            catch (Exception ex)
            {
                MainWindow.ShowError("文字フォーマット読み出しエラー", ex);
            }
        }
    }

    internal sealed class GraphemeChoice
    {
        public GraphemeChoice(int index, string text)
        {
            Index = index;
            Text = text;
        }

        public int Index { get; }
        public string Text { get; }
        public string DisplayText => $"{Index}: {Text}";
    }
}
