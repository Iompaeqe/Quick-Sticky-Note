using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace QuickSticky
{
    public class NoteModel
    {
        public int Version { get; set; } = 2;

        public string Title { get; set; } = "";
        public string Content { get; set; } = ""; // Legacy/plain text fallback.

        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 220;

        public List<NoteBlockModel> Blocks { get; set; } = new();

        public static NoteModel NewBlankAtCursor()
        {
            GetCursorPos(out var p);

            double left = p.X - 30;
            double top = p.Y - 30;

            var screenWidth = SystemParameters.VirtualScreenWidth;
            var screenHeight = SystemParameters.VirtualScreenHeight;

            left = Math.Max(0, Math.Min(left, screenWidth - 100));
            top = Math.Max(0, Math.Min(top, screenHeight - 100));

            return new NoteModel
            {
                Left = left,
                Top = top,
                Title = ""
            };
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
    }

    public class NoteBlockModel
    {
        public string Type { get; set; } = "Paragraph";

        public string Text { get; set; } = "";

        public string FileName { get; set; } = "";
        public string InkFileName { get; set; } = "";

        public double Width { get; set; }
        public double Height { get; set; }
    }
}
