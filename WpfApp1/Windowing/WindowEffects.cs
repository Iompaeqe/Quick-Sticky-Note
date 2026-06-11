using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickSticky
{
    internal static class WindowEffects
    {
        public static void Apply(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            ApplyDwmRoundedCorners(hwnd);
            EnableBlurBehind(window, hwnd);
        }

        public static void RefreshOpenWindows()
        {
            if (Application.Current == null)
                return;

            foreach (Window window in Application.Current.Windows)
                Apply(window);
        }

        private static void ApplyDwmRoundedCorners(IntPtr hwnd)
        {
            int preference = (int)NoteWindowSettings.CornerPreference;

            DwmSetWindowAttribute(
                hwnd,
                DWMWA_WINDOW_CORNER_PREFERENCE,
                ref preference,
                sizeof(int));
        }

        private static void EnableBlurBehind(Window window, IntPtr hwnd)
        {
            var source = HwndSource.FromHwnd(hwnd);

            if (source?.CompositionTarget != null)
                source.CompositionTarget.BackgroundColor = Colors.Transparent;

            var accent = new AccentPolicy
            {
                AccentState = ThemeManager.GetBlurEnabled()
                    ? AccentState.ACCENT_ENABLE_BLURBEHIND
                    : AccentState.ACCENT_DISABLED,
                AccentFlags = 0,
                GradientColor = ThemeManager.GetBlurTintColor(),
                AnimationId = 0
            };

            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(accent, ptr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };

                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(
            IntPtr hwnd,
            ref WindowCompositionAttributeData data);

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }
    }
}
