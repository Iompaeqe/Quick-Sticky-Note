using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickSticky
{
    public partial class ResizableImageBlock : UserControl
    {
        private const double MinResizeWidth = 40;

        private double _aspectRatio = 1;
        private bool _isHovered;
        private bool _isSelected;
        private bool _isResizing;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;

        public event EventHandler Selected;
        public event EventHandler DeleteRequested;
        public event EventHandler FitRequested;
        public event EventHandler SizeChangedByUser;

        public string FileName { get; private set; } = "";

        public double DisplayWidth => IsValidDimension(Width) ? Width : ActualWidth;
        public double DisplayHeight => IsValidDimension(Height) ? Height : ActualHeight;
        public double AspectRatio => _aspectRatio;

        public ResizableImageBlock()
        {
            InitializeComponent();
        }

        public ResizableImageBlock(string fileName, string imagePath, double width, double height)
            : this()
        {
            FileName = fileName;

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

            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            InvalidateMeasure();
            InvalidateArrange();
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            SetSelected(true);
            Selected?.Invoke(this, EventArgs.Empty);

            if (e.OriginalSource is DependencyObject source &&
                IsDescendantOf(source, ResizeHandle))
            {
                return;
            }

            if (e.ClickCount >= 2)
                FitRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void Root_KeyDown(object sender, KeyEventArgs e)
        {
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
            UpdateChrome();
            SizeChangedByUser?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void UpdateChrome()
        {
            var isActive = _isSelected || _isHovered || _isResizing;

            Chrome.BorderBrush = isActive
                ? new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF))
                : Brushes.Transparent;

            Chrome.Background = isActive
                ? new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(Color.FromArgb(0x06, 0x00, 0x00, 0x00));

            ResizeHandle.Opacity = isActive ? 1 : 0.72;
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
    }
}
