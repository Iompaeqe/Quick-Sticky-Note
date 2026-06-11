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

        public bool Removed { get; set; }
        public DateTime? RemovedAtUtc { get; set; }

        public double Left { get; set; } = 100;
        public double Top { get; set; } = 100;
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 220;

        public List<NoteBlockModel> Blocks { get; set; } = new();

        public static NoteModel NewBlankAtCursor()
        {
            GetCursorPos(out var p);

            // GetCursorPos reports pixels in this process's coordinate space. WPF
            // is System-DPI aware (no per-monitor manifest), so window coordinates
            // are device-independent units scaled by the system DPI. Convert with
            // the system DPI so the note lands under the cursor on scaled displays.
            double scale = GetSystemDpiScale();

            double left = p.X / scale - 30;
            double top = p.Y / scale - 30;

            // The virtual desktop starts at negative coordinates when monitors sit
            // to the left of, or above, the primary screen. Clamp within the full
            // virtual bounds (origin + size), not [0..], so notes opened on those
            // monitors stay put instead of snapping back onto the primary screen.
            double minLeft = SystemParameters.VirtualScreenLeft;
            double minTop = SystemParameters.VirtualScreenTop;
            double maxLeft = minLeft + SystemParameters.VirtualScreenWidth - 100;
            double maxTop = minTop + SystemParameters.VirtualScreenHeight - 100;

            left = Math.Max(minLeft, Math.Min(left, maxLeft));
            top = Math.Max(minTop, Math.Min(top, maxTop));

            return new NoteModel
            {
                Left = left,
                Top = top,
                Title = ""
            };
        }

        private static double GetSystemDpiScale()
        {
            try
            {
                uint dpi = GetDpiForSystem();

                if (dpi > 0)
                    return dpi / 96.0;
            }
            catch
            {
                // GetDpiForSystem is unavailable before Windows 10 1607; assume 100%.
            }

            return 1;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

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
