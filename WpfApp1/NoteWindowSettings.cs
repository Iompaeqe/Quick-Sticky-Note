using System;

namespace QuickSticky
{
    internal static class NoteWindowSettings
    {
        public const double DefaultWidth = 300;
        public const double DefaultHeight = 220;
        public const double MinValidWidth = 120;
        public const double MinValidHeight = 60;

        public const int ResizeBorderThickness = 8;

        public const int RequiredCloseClicks = 3;
        public static readonly TimeSpan CloseClickWindow = TimeSpan.FromSeconds(1.5);

        public const uint BlurTintColor = 0x00000000;

        public const DwmWindowCornerPreference CornerPreference =
            DwmWindowCornerPreference.Round;
    }

    internal enum DwmWindowCornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }
}
