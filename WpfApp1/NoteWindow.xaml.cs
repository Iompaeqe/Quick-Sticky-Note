using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace QuickSticky
{
    public partial class NoteWindow : Window
    {
        private const string DefaultTitlePlaceholder = "Quick Note";
        private const double MinInsertedImageWidth = 40;
        private const double EditorImagePadding = 18;

        private readonly string _path;

        private readonly DispatcherTimer _saveTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;

        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        private NoteModel _model;
        private bool _dirty;
        private bool _isLoading;
        private bool _isTitleEditing;
        private ResizableImageBlock _selectedImage;
        private ResizableImageBlock _activeDrawingImage;

        private int _closeClicks;
        private DateTime _firstClickTime;

        public NoteWindow(NoteModel model, string path)
        {
            InitializeComponent();

            _isLoading = true;

            _model = model;
            _path = path;

            Left = _model.Left;
            Top = _model.Top;

            Width = _model.Width > NoteWindowSettings.MinValidWidth
                ? _model.Width
                : NoteWindowSettings.DefaultWidth;

            Height = _model.Height > NoteWindowSettings.MinValidHeight
                ? _model.Height
                : NoteWindowSettings.DefaultHeight;

            TitleEditor.Text = _model.Title ?? "";
            Editor.Document = NoteDocumentConverter.ToFlowDocument(
                _model,
                _path,
                ConfigureImageBlock);

            UpdateWindowTitle();
            UpdateTitlePlaceholder();

            _isLoading = false;

            _saveTimer.Tick += (_, _) =>
            {
                if (_dirty)
                    SaveNow();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            WindowEffects.Apply(this);

            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowEffects.Apply(this);
            Editor.Focus();
        }

        private const int WM_NCHITTEST = 0x0084;

        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        private IntPtr WndProc(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (msg != WM_NCHITTEST)
                return IntPtr.Zero;

            int x = unchecked((short)(long)lParam);
            int y = unchecked((short)((long)lParam >> 16));

            Point pos = PointFromScreen(new Point(x, y));

            double border = NoteWindowSettings.ResizeBorderThickness;

            bool left = pos.X <= border;
            bool right = pos.X >= ActualWidth - border;
            bool top = pos.Y <= border;
            bool bottom = pos.Y >= ActualHeight - border;

            handled = true;

            if (top && left) return new IntPtr(HTTOPLEFT);
            if (top && right) return new IntPtr(HTTOPRIGHT);
            if (bottom && left) return new IntPtr(HTBOTTOMLEFT);
            if (bottom && right) return new IntPtr(HTBOTTOMRIGHT);
            if (left) return new IntPtr(HTLEFT);
            if (right) return new IntPtr(HTRIGHT);
            if (top) return new IntPtr(HTTOP);
            if (bottom) return new IntPtr(HTBOTTOM);

            handled = false;
            return IntPtr.Zero;
        }

        private void TitleEditor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
            {
                Window_MouseLeftButtonDown(sender, e);
                return;
            }

            _isTitleEditing = true;

            TitleEditor.IsReadOnly = false;
            TitleEditor.Focus();
            TitleEditor.SelectAll();

            e.Handled = true;
        }
        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isTitleEditing)
                return;

            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void TitleEditor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoading)
                return;

            _model.Title = TitleEditor.Text.Trim();
            UpdateWindowTitle();
            UpdateTitlePlaceholder();
            MarkDirty();
        }
        
        private void TitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            EndTitleEditing();
        }

        private void EndTitleEditing()
        {
            _isTitleEditing = false;
            TitleEditor.IsReadOnly = true;
        }

        private void Editor_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoading)
                return;

            MarkDirty();
        }

        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _activeDrawingImage != null)
            {
                ExitActiveDrawingMode();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && _selectedImage != null)
            {
                RemoveImage(_selectedImage);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                ClipboardHasImage())
            {
                PasteClipboardImage();
                e.Handled = true;
            }
        }

        private void Editor_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandlePointerOutsideActiveImage(e.OriginalSource as DependencyObject);
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandlePointerOutsideActiveImage(e.OriginalSource as DependencyObject);
        }

        private void HandlePointerOutsideActiveImage(DependencyObject source)
        {
            if (_activeDrawingImage != null &&
                (source == null || !IsDescendantOf(source, _activeDrawingImage)))
            {
                ExitActiveDrawingMode();
            }

            if (_selectedImage == null)
                return;

            if (source != null && IsDescendantOf(source, _selectedImage))
            {
                return;
            }

            ClearSelectedImage();
        }

        private void PasteClipboardImage()
        {
            try
            {
                var bitmap = Clipboard.GetImage();

                if (bitmap == null)
                    return;

                var fileName = NoteImageStorage.SaveClipboardBitmap(_path, bitmap);
                var imagePath = NoteImageStorage.GetImagePath(_path, fileName);
                var initialSize = GetInitialImageSize(bitmap);

                var image = new ResizableImageBlock(
                    fileName,
                    imagePath,
                    initialSize.Width,
                    initialSize.Height);

                ConfigureImageBlock(image);
                InsertImageAtCaret(image);
                MarkDirty();
            }
            catch
            {
                // Clipboard data can be transient or locked by another process.
            }
        }

        private void InsertImageAtCaret(ResizableImageBlock image)
        {
            ClearSelectedImage();

            if (!Editor.Selection.IsEmpty)
                Editor.Selection.Text = "";

            var imageBlock = NoteDocumentConverter.CreateImageBlock(image);
            var caret = Editor.CaretPosition;
            var paragraph = caret.Paragraph;

            if (paragraph == null || !ReferenceEquals(paragraph.Parent, Editor.Document))
            {
                Editor.Document.Blocks.Add(imageBlock);
                var trailingParagraph = NoteDocumentConverter.CreateParagraph("");
                Editor.Document.Blocks.Add(trailingParagraph);
                Editor.CaretPosition = trailingParagraph.ContentStart;
                Editor.Focus();
                return;
            }

            var beforeText = NoteDocumentConverter.GetText(paragraph.ContentStart, caret);
            var afterText = NoteDocumentConverter.GetText(caret, paragraph.ContentEnd);

            if (!string.IsNullOrEmpty(beforeText))
            {
                Editor.Document.Blocks.InsertBefore(
                    paragraph,
                    NoteDocumentConverter.CreateParagraph(beforeText));
            }

            Editor.Document.Blocks.InsertBefore(paragraph, imageBlock);

            var afterParagraph = NoteDocumentConverter.CreateParagraph(afterText);
            Editor.Document.Blocks.InsertBefore(paragraph, afterParagraph);
            Editor.Document.Blocks.Remove(paragraph);

            Editor.CaretPosition = afterParagraph.ContentStart;
            Editor.Focus();
        }

        private Size GetInitialImageSize(BitmapSource bitmap)
        {
            var originalWidth = bitmap.Width > 0 ? bitmap.Width : bitmap.PixelWidth;
            var originalHeight = bitmap.Height > 0 ? bitmap.Height : bitmap.PixelHeight;

            if (originalWidth <= 0 || originalHeight <= 0)
                return new Size(120, 90);

            var maxWidth = GetEditorImageFitWidth();
            var targetWidth = Math.Min(originalWidth, maxWidth);

            if (originalWidth >= MinInsertedImageWidth)
                targetWidth = Math.Max(MinInsertedImageWidth, targetWidth);

            var aspectRatio = originalWidth / originalHeight;
            return new Size(targetWidth, targetWidth / aspectRatio);
        }

        private double GetEditorImageFitWidth()
        {
            var width = Editor.ActualWidth -
                        Editor.Padding.Left -
                        Editor.Padding.Right -
                        EditorImagePadding;

            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                width = ActualWidth - 40;

            return Math.Max(MinInsertedImageWidth, width);
        }

        private void ConfigureImageBlock(ResizableImageBlock image)
        {
            image.Selected += Image_Selected;
            image.DeleteRequested += Image_DeleteRequested;
            image.FitRequested += Image_FitRequested;
            image.SizeChangedByUser += Image_SizeChangedByUser;
            image.DrawingModeStarted += Image_DrawingModeStarted;
            image.DrawingModeExited += Image_DrawingModeExited;
            image.AnnotationChanged += Image_AnnotationChanged;
        }

        private void Image_Selected(object sender, EventArgs e)
        {
            if (sender is ResizableImageBlock image)
                SelectImage(image);
        }

        private void Image_DeleteRequested(object sender, EventArgs e)
        {
            if (sender is ResizableImageBlock image)
                RemoveImage(image);
        }

        private void Image_FitRequested(object sender, EventArgs e)
        {
            if (sender is not ResizableImageBlock image)
                return;

            var width = GetEditorImageFitWidth();
            image.SetDisplaySize(width, width / image.AspectRatio);
            MarkDirty();
        }

        private void Image_SizeChangedByUser(object sender, EventArgs e)
        {
            MarkDirty();
        }

        private void Image_DrawingModeStarted(object sender, EventArgs e)
        {
            if (sender is not ResizableImageBlock image)
                return;

            if (_activeDrawingImage != null && !ReferenceEquals(_activeDrawingImage, image))
                _activeDrawingImage.ExitDrawingMode();

            _activeDrawingImage = image;
            SelectImage(image);
        }

        private void Image_DrawingModeExited(object sender, EventArgs e)
        {
            if (ReferenceEquals(_activeDrawingImage, sender))
                _activeDrawingImage = null;
        }

        private void Image_AnnotationChanged(object sender, EventArgs e)
        {
            MarkDirty();
        }

        private void SelectImage(ResizableImageBlock image)
        {
            if (_selectedImage != null && !ReferenceEquals(_selectedImage, image))
                _selectedImage.SetSelected(false);

            _selectedImage = image;
            _selectedImage.SetSelected(true);
        }

        private void ClearSelectedImage()
        {
            if (_selectedImage != null)
                _selectedImage.SetSelected(false);

            _selectedImage = null;
        }

        private void ExitActiveDrawingMode()
        {
            _activeDrawingImage?.ExitDrawingMode();
            _activeDrawingImage = null;
            Editor.Focus();
        }

        private void RemoveImage(ResizableImageBlock image)
        {
            var imageBlock = FindImageBlock(image);

            if (imageBlock == null)
                return;

            if (ReferenceEquals(_activeDrawingImage, image))
                _activeDrawingImage = null;

            NoteImageStorage.DeleteAssetFile(_path, image.InkFileName);

            var caretTarget = FindNextParagraph(imageBlock) ?? FindPreviousParagraph(imageBlock);

            Editor.Document.Blocks.Remove(imageBlock);
            NoteDocumentConverter.EnsureEditableDocument(Editor.Document);

            caretTarget ??= FindFirstParagraph();

            if (caretTarget != null)
                Editor.CaretPosition = caretTarget.ContentStart;

            ClearSelectedImage();
            Editor.Focus();
            MarkDirty();
        }

        private Block FindImageBlock(ResizableImageBlock image)
        {
            foreach (var block in Editor.Document.Blocks)
            {
                if (block is BlockUIContainer container &&
                    ReferenceEquals(container.Child, image))
                {
                    return container;
                }

                if (block is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is InlineUIContainer inlineContainer &&
                            ReferenceEquals(inlineContainer.Child, image))
                        {
                            return paragraph;
                        }
                    }
                }
            }

            return null;
        }

        private Paragraph FindNextParagraph(Block block)
        {
            var foundBlock = false;

            foreach (var candidate in Editor.Document.Blocks)
            {
                if (foundBlock && candidate is Paragraph paragraph)
                    return paragraph;

                if (ReferenceEquals(candidate, block))
                    foundBlock = true;
            }

            return null;
        }

        private Paragraph FindPreviousParagraph(Block block)
        {
            Paragraph previousParagraph = null;

            foreach (var candidate in Editor.Document.Blocks)
            {
                if (ReferenceEquals(candidate, block))
                    return previousParagraph;

                if (candidate is Paragraph paragraph)
                    previousParagraph = paragraph;
            }

            return null;
        }

        private Paragraph FindFirstParagraph()
        {
            foreach (var block in Editor.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                    return paragraph;
            }

            return null;
        }

        private static bool ClipboardHasImage()
        {
            try
            {
                return Clipboard.ContainsImage();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDescendantOf(DependencyObject source, DependencyObject target)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, target))
                    return true;

                if (source is FrameworkContentElement contentElement)
                {
                    source = contentElement.Parent;
                    continue;
                }

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

        private void UpdateWindowTitle()
        {
            Title = string.IsNullOrWhiteSpace(_model.Title)
                ? DefaultTitlePlaceholder
                : _model.Title;
        }

        private void UpdateTitlePlaceholder()
        {
            TitlePlaceholder.Visibility = string.IsNullOrWhiteSpace(TitleEditor.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Window_LocationOrSizeChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            _model.Left = Left;
            _model.Top = Top;
            _model.Width = Width;
            _model.Height = Height;

            MarkDirty();
        }

        private void MarkDirty()
        {
            _dirty = true;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveNow()
        {
            try
            {
                _model.Version = 2;
                _model.Blocks = NoteDocumentConverter.ToBlocks(Editor.Document, _path);
                _model.Content = NoteDocumentConverter.ToPlainText(_model.Blocks);

                NoteStorage.Save(_path, _model);
                _dirty = false;
                _saveTimer.Stop();
            }
            catch
            {
            }
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow.ShowSettings();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            var now = DateTime.UtcNow;

            if (_closeClicks == 0)
                _firstClickTime = now;

            _closeClicks++;

            if (now - _firstClickTime > NoteWindowSettings.CloseClickWindow)
            {
                _closeClicks = 1;
                _firstClickTime = now;
            }

            if (_closeClicks >= NoteWindowSettings.RequiredCloseClicks)
            {
                NoteStorage.Delete(_path);
                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _activeDrawingImage != null)
            {
                ExitActiveDrawingMode();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S &&
                Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                SaveNow();
                e.Handled = true;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_closeClicks < NoteWindowSettings.RequiredCloseClicks)
                SaveNow();

            base.OnClosing(e);
        }
    }
}
