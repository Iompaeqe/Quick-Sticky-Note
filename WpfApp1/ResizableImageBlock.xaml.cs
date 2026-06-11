using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickSticky
{
    public partial class ResizableImageBlock : UserControl
    {
        private const double MinResizeWidth = 40;
        private static readonly Color DefaultInkColor = Color.FromArgb(0xEF, 0xFF, 0xFF, 0xFF);
        private const double DefaultPenSize = 3;
        private const double DrawingToolbarReservedHeight = 40;
        private const double DrawingToolbarReservedWidth = 286;
        private const double ColorMarkerWidth = 7;

        private double _imageWidth;
        private double _imageHeight;
        private double _aspectRatio = 1;
        private Color _selectedInkColor = DefaultInkColor;
        private double _selectedPenSize = DefaultPenSize;
        private double _selectedColorProgress = 1;
        private bool _isHovered;
        private bool _isSelected;
        private bool _isResizing;
        private bool _isDrawingModeActive;
        private bool _isApplyingHistory;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private readonly Stack<InkHistoryItem> _undoStack = new();
        private readonly Stack<InkHistoryItem> _redoStack = new();

        public event EventHandler Selected;
        public event EventHandler DeleteRequested;
        public event EventHandler FitRequested;
        public event EventHandler SizeChangedByUser;
        public event EventHandler DrawingModeStarted;
        public event EventHandler DrawingModeExited;
        public event EventHandler AnnotationChanged;

        public string FileName { get; private set; } = "";
        public string InkFileName { get; private set; } = "";

        public double DisplayWidth => IsValidDimension(_imageWidth) ? _imageWidth : ImageSurface.ActualWidth;
        public double DisplayHeight => IsValidDimension(_imageHeight) ? _imageHeight : ImageSurface.ActualHeight;
        public double AspectRatio => _aspectRatio;
        public bool IsDrawingModeActive => _isDrawingModeActive;

        public ResizableImageBlock()
        {
            InitializeComponent();
            ConfigureInkCanvas();
        }

        public ResizableImageBlock(
            string fileName,
            string imagePath,
            double width,
            double height,
            string inkFileName = "",
            string inkPath = "")
            : this()
        {
            FileName = fileName;
            InkFileName = inkFileName ?? "";

            var bitmap = NoteImageStorage.LoadBitmap(imagePath);
            ImageElement.Source = bitmap;

            var naturalWidth = bitmap.Width > 0 ? bitmap.Width : bitmap.PixelWidth;
            var naturalHeight = bitmap.Height > 0 ? bitmap.Height : bitmap.PixelHeight;

            if (!IsValidDimension(width) || !IsValidDimension(height))
            {
                width = naturalWidth;
                height = naturalHeight;
            }

            _aspectRatio = height > 0 ? width / height : 1;

            if (!IsValidDimension(_aspectRatio))
                _aspectRatio = 1;

            SetDisplaySize(width, height);
            LoadInk(inkPath);
            UpdateChrome();
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            UpdateChrome();
        }

        public void SetDisplaySize(double width, double height)
        {
            if (!IsValidDimension(width) || !IsValidDimension(height))
                return;

            var oldWidth = IsValidDimension(_imageWidth) ? _imageWidth : 0;
            var oldHeight = IsValidDimension(_imageHeight) ? _imageHeight : 0;
            var nextWidth = Math.Max(1, width);
            var nextHeight = Math.Max(1, height);

            if (oldWidth > 0 &&
                oldHeight > 0 &&
                InkOverlay.Strokes.Count > 0 &&
                (Math.Abs(oldWidth - nextWidth) > double.Epsilon ||
                 Math.Abs(oldHeight - nextHeight) > double.Epsilon))
            {
                var scale = new Matrix(
                    nextWidth / oldWidth,
                    0,
                    0,
                    nextHeight / oldHeight,
                    0,
                    0);

                InkOverlay.Strokes.Transform(scale, true);
            }

            _imageWidth = nextWidth;
            _imageHeight = nextHeight;
            UpdateControlSize();
        }

        private void UpdateControlSize()
        {
            if (!IsValidDimension(_imageWidth) || !IsValidDimension(_imageHeight))
                return;

            Width = _isDrawingModeActive
                ? Math.Max(_imageWidth, DrawingToolbarReservedWidth)
                : _imageWidth;
            Height = _imageHeight + (_isDrawingModeActive ? DrawingToolbarReservedHeight : 0);
            ImageSurface.Width = _imageWidth;
            ImageSurface.Height = _imageHeight;
            Chrome.Width = _imageWidth;
            Chrome.Height = _imageHeight;
            InkOverlay.Width = _imageWidth;
            InkOverlay.Height = _imageHeight;
            InvalidateMeasure();
            InvalidateArrange();
        }

        public void ExitDrawingMode()
        {
            if (!_isDrawingModeActive)
                return;

            _isDrawingModeActive = false;
            InkOverlay.EditingMode = InkCanvasEditingMode.None;
            InkOverlay.IsHitTestVisible = false;
            Cursor = Cursors.Arrow;
            DrawingModeExited?.Invoke(this, EventArgs.Empty);
            UpdateChrome();
        }

        public string SaveInk(string notePath)
        {
            if (InkOverlay.Strokes.Count == 0)
            {
                NoteImageStorage.DeleteAssetFile(notePath, InkFileName);
                InkFileName = "";
                return "";
            }

            if (string.IsNullOrWhiteSpace(InkFileName))
                InkFileName = NoteImageStorage.GenerateInkFileName(FileName);

            var inkPath = NoteImageStorage.GetInkPath(notePath, InkFileName);
            Directory.CreateDirectory(NoteImageStorage.GetAssetFolderPath(notePath));

            using var stream = File.Create(inkPath);
            InkOverlay.Strokes.Save(stream);
            return InkFileName;
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            SetSelected(true);
            Selected?.Invoke(this, EventArgs.Empty);

            if (e.OriginalSource is DependencyObject source &&
                (IsDescendantOf(source, ResizeHandle) ||
                 IsDescendantOf(source, PenButton) ||
                 IsDescendantOf(source, DrawingToolbar)))
            {
                return;
            }

            if (e.ClickCount >= 2)
                FitRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void Root_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isDrawingModeActive &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.Key == Key.Z)
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        RedoInkAction();
                    else
                        UndoInkAction();

                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.Y)
                {
                    RedoInkAction();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key != Key.Delete)
                return;

            DeleteRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void Root_MouseEnter(object sender, MouseEventArgs e)
        {
            _isHovered = true;
            UpdateChrome();
        }

        private void Root_MouseLeave(object sender, MouseEventArgs e)
        {
            _isHovered = false;
            UpdateChrome();
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            InkOverlay.IsHitTestVisible = false;
            Focus();
            SetSelected(true);
            Selected?.Invoke(this, EventArgs.Empty);

            _isResizing = true;
            _resizeStartPoint = e.GetPosition(this);
            _resizeStartWidth = DisplayWidth;
            ResizeHandle.CaptureMouse();
            UpdateChrome();

            e.Handled = true;
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing || !ResizeHandle.IsMouseCaptured)
                return;

            var current = e.GetPosition(this);
            var horizontalChange = current.X - _resizeStartPoint.X;
            var verticalChange = (current.Y - _resizeStartPoint.Y) * _aspectRatio;
            var widthChange = Math.Abs(horizontalChange) >= Math.Abs(verticalChange)
                ? horizontalChange
                : verticalChange;

            var nextWidth = Math.Max(MinResizeWidth, _resizeStartWidth + widthChange);
            SetDisplaySize(nextWidth, nextWidth / _aspectRatio);
            SizeChangedByUser?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ResizeHandle.IsMouseCaptured)
                ResizeHandle.ReleaseMouseCapture();

            _isResizing = false;
            SetSelected(true);
            if (_isDrawingModeActive)
                InkOverlay.IsHitTestVisible = true;
            UpdateChrome();
            SizeChangedByUser?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDrawingModeActive)
            {
                ExitDrawingMode();
                e.Handled = true;
                return;
            }

            EnterDrawingMode();
            e.Handled = true;
        }

        private void ColorGradient_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PickColorFromGradient(e.GetPosition(ColorGradient));
            ColorGradient.CaptureMouse();
            e.Handled = true;
        }

        private void ColorGradient_MouseMove(object sender, MouseEventArgs e)
        {
            if (!ColorGradient.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            PickColorFromGradient(e.GetPosition(ColorGradient));
            e.Handled = true;
        }

        private void ColorGradient_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (ColorGradient.IsMouseCaptured)
                ColorGradient.ReleaseMouseCapture();

            e.Handled = true;
        }

        private void ColorGradient_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateColorMarker();
        }

        private void PenSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetPenSize(e.NewValue);
        }

        private void EraserToggle_Changed(object sender, RoutedEventArgs e)
        {
            ApplyInkEditingMode();
        }

        private void UpdateChrome()
        {
            var isActive = _isSelected || _isHovered || _isResizing || _isDrawingModeActive;

            Chrome.BorderBrush = isActive
                ? new SolidColorBrush(_isDrawingModeActive
                    ? Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)
                    : Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF))
                : Brushes.Transparent;

            Chrome.Background = isActive
                ? new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(Color.FromArgb(0x06, 0x00, 0x00, 0x00));

            ResizeHandle.Opacity = isActive ? 1 : 0.72;
            PenButton.Opacity = isActive ? 1 : 0.82;
            PenButton.Foreground = _isDrawingModeActive
                ? new SolidColorBrush(Color.FromArgb(0xFF, 0x11, 0x11, 0x11))
                : new SolidColorBrush(Color.FromArgb(0xEF, 0xFF, 0xFF, 0xFF));
            PenButton.Background = _isDrawingModeActive
                ? new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF))
                : Brushes.Transparent;

            DrawingToolbar.Visibility = _isDrawingModeActive
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateControlSize();
        }

        private void EnterDrawingMode()
        {
            _isDrawingModeActive = true;
            SetSelected(true);
            Selected?.Invoke(this, EventArgs.Empty);
            InkOverlay.IsHitTestVisible = true;
            ApplyInkEditingMode();
            InkOverlay.Focus();
            Cursor = Cursors.Pen;
            DrawingModeStarted?.Invoke(this, EventArgs.Empty);
            UpdateChrome();
        }

        private void ConfigureInkCanvas()
        {
            InkOverlay.DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = _selectedInkColor,
                Width = _selectedPenSize,
                Height = _selectedPenSize,
                FitToCurve = true,
                StylusTip = StylusTip.Ellipse
            };

            InkOverlay.StrokeCollected += (_, e) =>
            {
                if (_isApplyingHistory)
                    return;

                _undoStack.Push(new InkHistoryItem(InkHistoryAction.Add, e.Stroke));
                _redoStack.Clear();
                AnnotationChanged?.Invoke(this, EventArgs.Empty);
            };

            InkOverlay.StrokeErasing += (_, e) =>
            {
                if (_isApplyingHistory)
                    return;

                _undoStack.Push(new InkHistoryItem(InkHistoryAction.Remove, e.Stroke.Clone()));
                _redoStack.Clear();
                AnnotationChanged?.Invoke(this, EventArgs.Empty);
            };

            UpdateInkControls();
        }

        private void SetInkColor(Color color)
        {
            _selectedInkColor = color;

            var attributes = InkOverlay.DefaultDrawingAttributes.Clone();
            attributes.Color = color;
            InkOverlay.DefaultDrawingAttributes = attributes;

            UpdateInkControls();
        }

        private void SetPenSize(double size)
        {
            if (!IsValidDimension(size))
                return;

            _selectedPenSize = Math.Max(1, Math.Min(12, Math.Round(size)));

            if (InkOverlay == null)
                return;

            var attributes = InkOverlay.DefaultDrawingAttributes.Clone();
            attributes.Width = _selectedPenSize;
            attributes.Height = _selectedPenSize;
            InkOverlay.DefaultDrawingAttributes = attributes;

            UpdateInkControls();
        }

        private void ApplyInkEditingMode()
        {
            if (InkOverlay == null)
                return;

            if (!_isDrawingModeActive)
            {
                InkOverlay.EditingMode = InkCanvasEditingMode.None;
                return;
            }

            InkOverlay.EditingMode = EraserToggle.IsChecked == true
                ? InkCanvasEditingMode.EraseByStroke
                : InkCanvasEditingMode.Ink;

            Cursor = EraserToggle.IsChecked == true ? Cursors.Cross : Cursors.Pen;
        }

        private void PickColorFromGradient(Point point)
        {
            var width = Math.Max(1, ColorGradient.ActualWidth);
            _selectedColorProgress = Math.Max(0, Math.Min(1, point.X / width));
            SetInkColor(GetGradientColor(_selectedColorProgress));
            EraserToggle.IsChecked = false;
            ApplyInkEditingMode();
        }

        private Color GetGradientColor(double progress)
        {
            progress = Math.Max(0, Math.Min(1, progress));
            var stops = new[]
            {
                Color.FromRgb(0xFF, 0x4F, 0x4F),
                Color.FromRgb(0xFF, 0xD8, 0x4D),
                Color.FromRgb(0x7C, 0xFF, 0x8A),
                Color.FromRgb(0x62, 0xD2, 0xFF),
                Color.FromRgb(0x8E, 0x78, 0xFF),
                Color.FromRgb(0xFF, 0x67, 0xD8),
                Color.FromRgb(0xFF, 0xFF, 0xFF)
            };

            var scaled = progress * (stops.Length - 1);
            var index = Math.Min(stops.Length - 2, (int)Math.Floor(scaled));
            var local = scaled - index;

            return Color.FromArgb(
                0xEF,
                Interpolate(stops[index].R, stops[index + 1].R, local),
                Interpolate(stops[index].G, stops[index + 1].G, local),
                Interpolate(stops[index].B, stops[index + 1].B, local));
        }

        private void UpdateInkControls()
        {
            UpdateColorMarker();

            if (PenSizeText != null)
                PenSizeText.Text = _selectedPenSize.ToString("0");

            if (PenSizeSlider != null &&
                Math.Abs(PenSizeSlider.Value - _selectedPenSize) > double.Epsilon)
            {
                PenSizeSlider.Value = _selectedPenSize;
            }

        }

        private void UndoInkAction()
        {
            if (_undoStack.Count == 0)
                return;

            var action = _undoStack.Pop();
            _isApplyingHistory = true;

            try
            {
                if (action.Action == InkHistoryAction.Add)
                    RemoveStrokeIfPresent(action.Stroke);
                else
                    AddStrokeIfMissing(action.Stroke);
            }
            finally
            {
                _isApplyingHistory = false;
            }

            _redoStack.Push(action);
            AnnotationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RedoInkAction()
        {
            if (_redoStack.Count == 0)
                return;

            var action = _redoStack.Pop();
            _isApplyingHistory = true;

            try
            {
                if (action.Action == InkHistoryAction.Add)
                    AddStrokeIfMissing(action.Stroke);
                else
                    RemoveStrokeIfPresent(action.Stroke);
            }
            finally
            {
                _isApplyingHistory = false;
            }

            _undoStack.Push(action);
            AnnotationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateColorMarker()
        {
            if (ColorMarker == null || ColorGradient == null)
                return;

            var gradientWidth = ColorGradient.ActualWidth > 0
                ? ColorGradient.ActualWidth
                : 112;

            var left = _selectedColorProgress * Math.Max(0, gradientWidth - ColorMarkerWidth);

            ColorMarker.Margin = new Thickness(left, -2, 0, -2);
            ColorMarker.Background = new SolidColorBrush(_selectedInkColor);
        }

        private void AddStrokeIfMissing(Stroke stroke)
        {
            if (!ContainsStroke(stroke))
                InkOverlay.Strokes.Add(stroke);
        }

        private void RemoveStrokeIfPresent(Stroke stroke)
        {
            if (ContainsStroke(stroke))
                InkOverlay.Strokes.Remove(stroke);
        }

        private bool ContainsStroke(Stroke stroke)
        {
            foreach (var existing in InkOverlay.Strokes)
            {
                if (ReferenceEquals(existing, stroke))
                    return true;
            }

            return false;
        }

        private static byte Interpolate(byte start, byte end, double amount)
        {
            return (byte)Math.Round(start + (end - start) * amount);
        }

        private void LoadInk(string inkPath)
        {
            if (string.IsNullOrWhiteSpace(inkPath) || !File.Exists(inkPath))
                return;

            try
            {
                using var stream = File.OpenRead(inkPath);
                InkOverlay.Strokes = new StrokeCollection(stream);
            }
            catch
            {
                InkOverlay.Strokes = new StrokeCollection();
            }
        }

        private static bool IsDescendantOf(DependencyObject source, DependencyObject target)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, target))
                    return true;

                try
                {
                    source = VisualTreeHelper.GetParent(source);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsValidDimension(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value) &&
                   value > 0;
        }

        private enum InkHistoryAction
        {
            Add,
            Remove
        }

        private sealed class InkHistoryItem
        {
            public InkHistoryItem(InkHistoryAction action, Stroke stroke)
            {
                Action = action;
                Stroke = stroke;
            }

            public InkHistoryAction Action { get; }
            public Stroke Stroke { get; }
        }
    }
}
