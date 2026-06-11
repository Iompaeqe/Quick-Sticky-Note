using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace QuickSticky
{
    internal static class NoteDocumentConverter
    {
        private const string ParagraphType = "Paragraph";
        private const string ImageType = "Image";

        public static FlowDocument ToFlowDocument(
            NoteModel model,
            string notePath,
            Action<ResizableImageBlock> configureImage)
        {
            var document = CreateDocument();
            var sourceBlocks = model.Blocks ?? new List<NoteBlockModel>();

            if (sourceBlocks.Count > 0)
            {
                foreach (var block in sourceBlocks)
                {
                    AddModelBlock(document, notePath, block, configureImage);
                }
            }
            else if (!string.IsNullOrEmpty(model.Content))
            {
                document.Blocks.Add(CreateParagraph(model.Content));
            }

            EnsureEditableDocument(document);
            return document;
        }

        public static List<NoteBlockModel> ToBlocks(FlowDocument document, string notePath)
        {
            var blocks = new List<NoteBlockModel>();

            foreach (var block in document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    if (TryGetParagraphImage(paragraph, out var paragraphImage))
                    {
                        blocks.Add(CreateImageModel(paragraphImage, notePath));
                        continue;
                    }

                    blocks.Add(new NoteBlockModel
                    {
                        Type = ParagraphType,
                        Text = GetText(paragraph.ContentStart, paragraph.ContentEnd)
                    });

                    continue;
                }

                if (block is BlockUIContainer container &&
                    container.Child is ResizableImageBlock image)
                {
                    blocks.Add(CreateImageModel(image, notePath));

                    continue;
                }

                var fallbackText = TryGetBlockText(block);

                if (!string.IsNullOrEmpty(fallbackText))
                {
                    blocks.Add(new NoteBlockModel
                    {
                        Type = ParagraphType,
                        Text = fallbackText
                    });
                }
            }

            if (blocks.Count == 0)
            {
                blocks.Add(new NoteBlockModel
                {
                    Type = ParagraphType,
                    Text = ""
                });
            }

            return blocks;
        }

        public static string ToPlainText(IEnumerable<NoteBlockModel> blocks)
        {
            var parts = new List<string>();

            foreach (var block in blocks)
            {
                if (IsImage(block))
                {
                    parts.Add("[Image]");
                    continue;
                }

                parts.Add(block.Text ?? "");
            }

            return string.Join(Environment.NewLine + Environment.NewLine, parts);
        }

        public static Paragraph CreateParagraph(string text)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0)
            };

            paragraph.Inlines.Add(new Run(text ?? ""));
            return paragraph;
        }

        public static Block CreateImageBlock(ResizableImageBlock image)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 4, 0, 4),
                TextAlignment = TextAlignment.Left
            };

            paragraph.Inlines.Add(new InlineUIContainer(image)
            {
                BaselineAlignment = BaselineAlignment.Bottom
            });

            return paragraph;
        }

        public static string GetText(TextPointer start, TextPointer end)
        {
            return TrimParagraphBreak(new TextRange(start, end).Text);
        }

        public static void EnsureEditableDocument(FlowDocument document)
        {
            if (document.Blocks.Count == 0)
                document.Blocks.Add(CreateParagraph(""));
        }

        private static FlowDocument CreateDocument()
        {
            var document = new FlowDocument
            {
                Background = Brushes.Transparent,
                ColumnWidth = 100000,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                PagePadding = new Thickness(0)
            };

            document.SetResourceReference(TextElement.ForegroundProperty, "TextPrimaryBrush");

            return document;
        }

        private static void AddModelBlock(
            FlowDocument document,
            string notePath,
            NoteBlockModel block,
            Action<ResizableImageBlock> configureImage)
        {
            if (IsImage(block))
            {
                AddImageBlock(document, notePath, block, configureImage);
                return;
            }

            if (IsParagraph(block))
            {
                document.Blocks.Add(CreateParagraph(block.Text));
                return;
            }

            if (!string.IsNullOrEmpty(block.Text))
                document.Blocks.Add(CreateParagraph(block.Text));
        }

        private static void AddImageBlock(
            FlowDocument document,
            string notePath,
            NoteBlockModel block,
            Action<ResizableImageBlock> configureImage)
        {
            if (string.IsNullOrWhiteSpace(block.FileName))
                return;

            var imagePath = NoteImageStorage.GetImagePath(notePath, block.FileName);
            var inkPath = string.IsNullOrWhiteSpace(block.InkFileName)
                ? ""
                : NoteImageStorage.GetInkPath(notePath, block.InkFileName);

            if (!File.Exists(imagePath))
            {
                document.Blocks.Add(CreateParagraph("[Missing image]"));
                return;
            }

            try
            {
                var image = new ResizableImageBlock(
                    Path.GetFileName(block.FileName),
                    imagePath,
                    block.Width,
                    block.Height,
                    Path.GetFileName(block.InkFileName),
                    inkPath);

                configureImage?.Invoke(image);
                document.Blocks.Add(CreateImageBlock(image));
            }
            catch
            {
                document.Blocks.Add(CreateParagraph("[Missing image]"));
            }
        }

        private static bool IsParagraph(NoteBlockModel block)
        {
            return string.Equals(block.Type, ParagraphType, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImage(NoteBlockModel block)
        {
            return string.Equals(block.Type, ImageType, StringComparison.OrdinalIgnoreCase);
        }

        private static NoteBlockModel CreateImageModel(ResizableImageBlock image, string notePath)
        {
            var inkFileName = image.SaveInk(notePath);

            return new NoteBlockModel
            {
                Type = ImageType,
                FileName = image.FileName,
                InkFileName = inkFileName,
                Width = image.DisplayWidth,
                Height = image.DisplayHeight
            };
        }

        private static bool TryGetParagraphImage(
            Paragraph paragraph,
            out ResizableImageBlock image)
        {
            foreach (var inline in paragraph.Inlines)
            {
                if (inline is InlineUIContainer container &&
                    container.Child is ResizableImageBlock block)
                {
                    image = block;
                    return true;
                }
            }

            image = null;
            return false;
        }

        private static string TrimParagraphBreak(string text)
        {
            if (text.EndsWith("\r\n", StringComparison.Ordinal))
                return text[..^2];

            if (text.EndsWith("\n", StringComparison.Ordinal) ||
                text.EndsWith("\r", StringComparison.Ordinal))
            {
                return text[..^1];
            }

            return text;
        }

        private static string TryGetBlockText(Block block)
        {
            try
            {
                return GetText(block.ContentStart, block.ContentEnd);
            }
            catch
            {
                return "";
            }
        }
    }
}
