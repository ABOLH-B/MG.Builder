using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MasonMBR.Core;

namespace MasonMBR
{
    public partial class MasonForm : Form
    {
        string   _nasmPath;
        string   _iconPath;
        byte[]   _imgPixels;
        bool     _imgMode;
        byte[][] _imgFrames;
        int[]    _imgDelays;
        Builder  _builder;
        byte[]  _screenImgData;
        byte[]  _videoData;
        string  _videoExt;
        bool    _screenIsVideo;
        byte[]  _audioData;
        string  _audioExt;
        System.Threading.Thread _gifThread;
        int _gifGen;

        Color _fgColor  = Color.White;
        Color _bgColor  = Color.Black;
        Color _scrColor = Color.Black;

        static readonly Color[] BiosPal = {
            Color.Black,
            Color.FromArgb(0,   0,   170),
            Color.FromArgb(0,   170, 0),
            Color.FromArgb(0,   170, 170),
            Color.FromArgb(170, 0,   0),
            Color.FromArgb(170, 0,   170),
            Color.FromArgb(170, 85,  0),
            Color.FromArgb(170, 170, 170),
            Color.FromArgb(85,  85,  85),
            Color.FromArgb(85,  85,  255),
            Color.FromArgb(85,  255, 85),
            Color.FromArgb(85,  255, 255),
            Color.FromArgb(255, 85,  85),
            Color.FromArgb(255, 85,  255),
            Color.FromArgb(255, 255, 85),
            Color.White,
        };

        public MasonForm()
        {
            InitializeComponent();
            MBRTextBox1.MaxLength = 450;
            MBRTextBox1.KeyPress += _MBRKeyPress;
            StartInit();
        }

        void _MBRKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            if (e.KeyChar > 127)
                e.Handled = true;
        }

        async void StartInit()
        {
            await Init();
        }

        void PickColor(int which)
        {
            using (var dlg = new ColorDialog())
            {
                dlg.FullOpen = true;
                dlg.Color    = which == 0 ? _fgColor : which == 1 ? _bgColor : _scrColor;
                if (dlg.ShowDialog() != DialogResult.OK) return;

                if (which == 0) { _fgColor  = dlg.Color; _btnFg.FillColor  = dlg.Color; }
                else if (which == 1) { _bgColor  = dlg.Color; _btnBg.FillColor  = dlg.Color; }
                else                 { _scrColor = dlg.Color; _btnScr.FillColor = dlg.Color; }
            }
        }

        void SetTextControlsEnabled(bool on)
        {
            _btnFg.Enabled  = on;
            _btnBg.Enabled  = on;
            _btnScr.Enabled = on;
        }

        static byte NearestBios(Color c, bool bgOnly)
        {
            int limit = bgOnly ? 8 : 16;
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < limit; i++)
            {
                int dr = c.R - BiosPal[i].R;
                int dg = c.G - BiosPal[i].G;
                int db = c.B - BiosPal[i].B;
                int d  = dr*dr + dg*dg + db*db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return (byte)best;
        }

        async Task Init()
        {
            await Task.Run(() =>
            {
                string[] paths = {
                    "nasm.dll",
                    Path.Combine(Application.StartupPath,        "nasm.dll"),
                    Path.Combine(Directory.GetCurrentDirectory(), "nasm.dll"),
                    Path.Combine(Environment.CurrentDirectory,    "nasm.dll")
                };
                foreach (var p in paths)
                    if (File.Exists(p)) { _nasmPath = p; break; }
            });

            if (_nasmPath != null)
                _builder = new Builder(_nasmPath);

            if (guna2ComboBox1.InvokeRequired)
                guna2ComboBox1.Invoke(new Action(FillCombo));
            else
                FillCombo();
        }

        void FillCombo()
        {
            guna2ComboBox1.Items.AddRange(new object[] { ".exe", ".scr", ".com", ".pif", ".bat", ".cmd" });
            guna2ComboBox1.SelectedIndex = 0;
            guna2TextBox1.Enabled = false;
        }

        async void Form1_Load(object sender, EventArgs e)
        {
            string tip = await Task.Run(() => Core.AppMeta._GetBuildSignature());
            guna2HtmlToolTip1.SetToolTip(label1, tip);
        }

        async void BuildButton1_Click(object sender, EventArgs e)
        {
            if (!CanBuild()) return;

            string fmt = guna2ComboBox1.SelectedItem != null ? guna2ComboBox1.SelectedItem.ToString() : ".exe";
            var sfd = new SaveFileDialog
            {
                Filter   = fmt.Substring(1).ToUpper() + " Files (*" + fmt + ")|*" + fmt,
                FileName = "HitDevice" + fmt,
                Title    = "Save"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            BuildButton1.Enabled = false;

            bool   bsod   = SwBSOD.Checked;
            bool   gdi    = SwGDI.Checked;
            string mbrTxt = _imgMode ? "" : MBRTextBox1.Text;
            string gdiTxt = gdi ? guna2TextBox1.Text : "";

            byte fg  = NearestBios(_fgColor,  false);
            byte bg  = NearestBios(_bgColor,  true);
            byte scr = NearestBios(_scrColor, true);

            await Task.Run(() =>
            {
                var cfg = new BuildConfig
                {
                    Bsod          = bsod,
                    Gdi           = gdi,
                    ImageMode     = _imgMode,
                    Text          = mbrTxt,
                    GdiText       = gdiTxt,
                    ImagePixels   = _imgPixels,
                    ImageFrames   = _imgFrames,
                    FrameDelays   = _imgDelays,
                    IconPath      = _iconPath,
                    TextFg        = fg,
                    TextBg        = bg,
                    ScrBg         = scr,
                    LockMouse     = SwLockMouse.Checked,
                    LockKeyboard  = SwLockKeyboard.Checked,
                    ShowScreenImg = !_screenIsVideo && _screenImgData != null,
                    PlayVideo     = _screenIsVideo  && _videoData     != null,
                    PlayAudio     = _audioData != null,
                    ScreenImgData = _screenImgData,
                    VideoData     = _videoData,
                    VideoExt      = _videoExt ?? "mp4",
                    AudioData     = _audioData,
                    AudioExt      = _audioExt ?? "mp3",
                };
                _builder.Run(cfg, sfd.FileName);
            });

            BuildButton1.Enabled = true;
        }

        bool CanBuild()
        {
            if (_nasmPath == null)
            {
                MessageBox.Show(Localization.T("nasm.dll not found"), Localization.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
                return false;
            }
            if (_imgMode && _imgPixels == null)
            {
                MessageBox.Show(Localization.T("No image selected"), Localization.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
                return false;
            }
            return true;
        }

        void pictureBox2_Click(object sender, EventArgs e)
        {
            PickImage();
        }
        void label2_Click(object sender, EventArgs e)      
        { 
            PickImage(); 
        }

        void PickImage()
        {
            if (_imgMode && _imgPixels != null)
            {
                var ans = MessageBox.Show(
                    "[Yes] pick a new image\n[No] remove image and go back to text\n[Cancel] keep current image",
                    "Image Loaded", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);

                if (ans == DialogResult.No)     { ClearImage(); return; }
                if (ans == DialogResult.Cancel) return;
            }

            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Images|*.bmp;*.png;*.jpg;*.jpeg;*.gif;*.ico|All files|*.*";
                ofd.Title  = "Select MBR Image";
                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    _ClearScreenContent();
                    return;
                }

                string ext = Path.GetExtension(ofd.FileName).ToLower();

                if (ext == ".gif")
                {
                    byte[][] gFrames; int[] gDelays; string gErr;
                    if (VgaImage.TryConvertGif(ofd.FileName, out gFrames, out gDelays, out gErr))
                        { SetAnimated(gFrames, gDelays); return; }
                    if (gErr != null)
                    {
                        MessageBox.Show(gErr, "Image Rejected", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
                        return;
                    }
                }

                byte[] px; string err;
                if (!VgaImage.TryConvert(ofd.FileName, out px, out err))
                {
                    MessageBox.Show(err, "Image Rejected", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
                    return;
                }
                SetStatic(px);
            }
        }

        void SetAnimated(byte[][] frames, int[] delays)
        {
            _imgFrames = frames; _imgDelays = delays; _imgPixels = frames[0]; _imgMode = true;
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            label2.Visible       = false;
            MBRTextBox1.Enabled  = false;
            MBRTextBox1.Text     = "";
            SetTextControlsEnabled(false);
            _StartGifPreview(frames, delays);
            SwLockMouse.Checked    = true;
            SwLockKeyboard.Checked = true;
        }

        void _StartGifPreview(byte[][] frames, int[] delays)
        {
            int myGen = System.Threading.Interlocked.Increment(ref _gifGen);

            _gifThread = new System.Threading.Thread(() =>
            {
                int idx = 0;
                var bitmaps = new Bitmap[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                    bitmaps[i] = VgaImage.Preview(frames[i]);

                while (System.Threading.Thread.VolatileRead(ref _gifGen) == myGen)
                {
                    Bitmap cur = bitmaps[idx];
                    try
                    {
                        if (pictureBox2.IsHandleCreated)
                            pictureBox2.Invoke(new Action<Bitmap>(b => pictureBox2.Image = b), cur);
                    }
                    catch { break; }

                    int ms = delays[idx] * 10;
                    if (ms < 50) ms = 100;
                    System.Threading.Thread.Sleep(ms);
                    idx = (idx + 1) % frames.Length;
                }

                foreach (var b in bitmaps)
                    try { b.Dispose(); } catch { }
            });
            _gifThread.IsBackground = true;
            _gifThread.Start();
        }

        void SetStatic(byte[] px)
        {
            _imgFrames = null; _imgDelays = null; _imgPixels = px; _imgMode = true;
            pictureBox2.Image    = VgaImage.Preview(px);
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            label2.Visible       = false;
            MBRTextBox1.Enabled  = false;
            MBRTextBox1.Text     = "";
            SetTextControlsEnabled(false);
            SwLockMouse.Checked    = true;
            SwLockKeyboard.Checked = true;
        }

        void ClearImage()
        {
            System.Threading.Interlocked.Increment(ref _gifGen);
            _imgMode = false; _imgPixels = null; _imgFrames = null; _imgDelays = null;
            pictureBox2.Image   = null;
            label2.Visible      = true;
            MBRTextBox1.Enabled = true;
            SetTextControlsEnabled(true);
        }

        void ICONCheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (!ICONCheckBox1.Checked)
            {
                _iconPath = null;
                pictureBox1.Image = null;
                guna2ComboBox1.Enabled = true;
                return;
            }

            var ofd = new OpenFileDialog { Filter = "Icons|*.ico", Title = "Select Icon" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _iconPath = ofd.FileName;
                try { pictureBox1.Image = new Icon(_iconPath).ToBitmap(); }
                catch { pictureBox1.Image = null; }
                guna2ComboBox1.SelectedIndex = 0;
                guna2ComboBox1.Enabled = false;
            }
            else
            {
                ICONCheckBox1.Checked  = false;
                guna2ComboBox1.Enabled = true;
            }
        }

        void guna2ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string fmt = guna2ComboBox1.SelectedItem != null ? guna2ComboBox1.SelectedItem.ToString() : ".exe";
            bool pe = fmt == ".exe" || fmt == ".scr";

            if (ICONCheckBox1.Checked && !pe)
            {
                MessageBox.Show("Cannot change format to non-PE while an icon is selected. Uncheck the icon first",
                    "Format Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                guna2ComboBox1.SelectedIndex = 0;
                return;
            }

            ICONCheckBox1.Enabled = pe;
        }

        void label1_Click(object sender, EventArgs e)
        {
            Core.AppMeta._InitPlatformChannels();
        }

        void SwGDI_CheckedChanged(object sender, EventArgs e)
        {
            guna2TextBox1.Enabled = SwGDI.Checked;
            if (!SwGDI.Checked) guna2TextBox1.Text = "";
        }

        void _btnFg_Click(object sender, EventArgs e)
        {
            PickColor(0);
        }

        void _btnBg_Click(object sender, EventArgs e)
        {
            PickColor(1);
        }

        void _btnScr_Click(object sender, EventArgs e)
        {
            PickColor(2);
        }

        void MBRTextBox1_TextChanged(object sender, EventArgs e)
        {
            string cur = MBRTextBox1.Text;
            bool dirty = false;
            foreach (char c in cur)
                if (c > 127) { dirty = true; break; }

            if (!dirty) return;

            var sb = new System.Text.StringBuilder(cur.Length);
            foreach (char c in cur)
                if (c <= 127) sb.Append(c);

            string filtered = sb.ToString();
            int pos = MBRTextBox1.SelectionStart - (cur.Length - filtered.Length);
            MBRTextBox1.TextChanged -= MBRTextBox1_TextChanged;
            MBRTextBox1.Text = filtered;
            MBRTextBox1.SelectionStart = pos < 0 ? 0 : pos > filtered.Length ? filtered.Length : pos;
            MBRTextBox1.TextChanged += MBRTextBox1_TextChanged;
        }

        void SwBSOD_CheckedChanged(object sender, EventArgs e) { }
        void pictureBox1_Click(object sender, EventArgs e) { }
        void guna2CustomGradientPanel2_Paint(object sender, PaintEventArgs e) { }
        void guna2HtmlToolTip1_Popup(object sender, PopupEventArgs e) { }

        void label4_Click(object sender, EventArgs e)     
        { 
            _PickScreenContent(); 
        }
        void pictureBox3_Click(object sender, EventArgs e) 
        {
            _PickScreenContent();
        }
        void guna2Button4_Click(object sender, EventArgs e)
        { 
            _PickScreenContent(); 
        }
        void label5_Click(object sender, EventArgs e)      
        {
            _PickScreenContent(); 
        }

        void _ClearScreenContent()
        {
            _screenImgData = null;
            _videoData     = null;
            _videoExt      = null;
            _screenIsVideo = false;
            pictureBox3.Image     = null;
            pictureBox3.BackColor = System.Drawing.Color.Transparent;
            label4.Text    = Localization.T("Select an image or GIF to \r\ndisplay in full-screen mode");
            label4.Visible = true;
            label5.Text    = Localization.T("No video");
            SwGDI.Enabled  = true;
        }

        void _PickScreenContent()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = Localization.IsArabic
                    ? "صور وفيديو|*.bmp;*.png;*.jpg;*.jpeg;*.gif;*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.mpeg;*.mpg|كل الملفات|*.*"
                    : "Images & Video|*.bmp;*.png;*.jpg;*.jpeg;*.gif;*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.mpeg;*.mpg|All files|*.*";
                ofd.Title = Localization.T("Select Screen Image");
                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    if (_imgMode) ClearImage();
                    return;
                }

                string ext = System.IO.Path.GetExtension(ofd.FileName).TrimStart('.').ToLower();
                bool isVid = ext == "mp4" || ext == "avi" || ext == "mkv" || ext == "mov" ||
                             ext == "wmv" || ext == "flv" || ext == "webm" || ext == "mpeg" || ext == "mpg";
                try
                {
                    _ClearScreenContent();

                    if (isVid)
                    {
                        _videoData     = File.ReadAllBytes(ofd.FileName);
                        _videoExt      = ext;
                        _screenIsVideo = true;
                        label5.Text    = System.IO.Path.GetFileName(ofd.FileName);

                        Image thumb = _VideoThumb(ofd.FileName);
                        if (thumb != null)
                        {
                            pictureBox3.Image    = thumb;
                            pictureBox3.SizeMode = PictureBoxSizeMode.StretchImage;
                            pictureBox3.BackColor = System.Drawing.Color.Black;
                            label4.Visible = false;
                        }
                        else
                        {
                            pictureBox3.Image     = null;
                            pictureBox3.BackColor = System.Drawing.Color.FromArgb(20, 20, 20);
                            label4.Text    = System.IO.Path.GetFileName(ofd.FileName);
                            label4.Visible = true;
                        }
                    }
                    else
                    {
                        _screenImgData         = File.ReadAllBytes(ofd.FileName);
                        _screenIsVideo         = false;
                        pictureBox3.Image      = Image.FromFile(ofd.FileName);
                        pictureBox3.SizeMode   = PictureBoxSizeMode.StretchImage;
                        label4.Visible         = false;
                        label5.Text            = System.IO.Path.GetFileName(ofd.FileName);
                    }

                    SwLockMouse.Checked    = true;
                    SwLockKeyboard.Checked = true;
                    SwGDI.Checked          = false;
                    SwGDI.Enabled          = false;
                }
                catch { _ClearScreenContent(); }
            }
        }

        Image _GetVideoThumbnail(string path) { return null; }

        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            [System.Runtime.InteropServices.PreserveSig]
            int GetImage([System.Runtime.InteropServices.In] System.Drawing.Size sz,
                         [System.Runtime.InteropServices.In] uint flags,
                         [System.Runtime.InteropServices.Out] out IntPtr phbm);
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string pszPath, IntPtr pbc,
            [System.Runtime.InteropServices.In] ref Guid riid,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        Image _VideoThumb(string path)
        {
            try
            {
                var iid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
                IShellItemImageFactory fac;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out fac);
                IntPtr hbm;
                int hr = fac.GetImage(new System.Drawing.Size(pictureBox3.Width, pictureBox3.Height), 0, out hbm);
                if (hr != 0 || hbm == IntPtr.Zero) return null;
                var bmp = System.Drawing.Image.FromHbitmap(hbm);
                return bmp;
            }
            catch { return null; }
        }

        void SwLockMouse_CheckedChanged(object sender, EventArgs e)     
        {

        }
        void SwLockKeyboard_CheckedChanged(object sender, EventArgs e)  
        { 

        }

        void btnPickAudio_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Audio|*.wav;*.mp3;*.ogg;*.flac;*.aac;*.wma|All files|*.*";
                ofd.Title  = Localization.T("Select Audio File");
                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    _audioData = null;
                    _audioExt  = null;
                    lblAudioName.Text = Localization.T("No audio selected");
                    return;
                }

                string ext = System.IO.Path.GetExtension(ofd.FileName).TrimStart('.').ToLower();
                if (!_CanPlayAudio(ofd.FileName, ext))
                {
                    MessageBox.Show(
                        Localization.T("Cannot play this audio file. Make sure the codec is installed."),
                        Localization.T("Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    _audioData = File.ReadAllBytes(ofd.FileName);
                    _audioExt  = string.IsNullOrEmpty(ext) ? "mp3" : ext;
                    lblAudioName.Text = System.IO.Path.GetFileName(ofd.FileName);
                }
                catch { _audioData = null; }
            }
        }

        bool _CanPlayAudio(string path, string ext)
        {
            if (ext == "wav") return true;
            try
            {
                var sb = new System.Text.StringBuilder(256);
                string alias = "_chk" + System.Environment.TickCount.ToString();
                string openType = (ext == "mp3" || ext == "wma" || ext == "aac" || ext == "m4a")
                    ? "type mpegvideo" : "";
                string cmd = string.IsNullOrEmpty(openType)
                    ? "open \"" + path + "\" alias " + alias
                    : "open \"" + path + "\" " + openType + " alias " + alias;

                int rc = _MciSend(cmd, sb, 256);
                if (rc != 0) return false;
                _MciSend("close " + alias, null, 0);
                return true;
            }
            catch { return false; }
        }

        [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern int _MciSend(string cmd, System.Text.StringBuilder ret, int retLen);

        void lblAudioName_Click(object sender, EventArgs e) 
        { 
            btnPickAudio_Click(sender, e); 
        }

        private void lblSwGDI_Click(object sender, EventArgs e)
        {

        }

        private void lblSwBSOD_Click(object sender, EventArgs e) 
        { 

        }

        void btnLang_Click(object sender, EventArgs e)
        {
            Localization.IsArabic = !Localization.IsArabic;
            btnLang.Text = Localization.IsArabic ? "EN" : "AR";
            _ApplyLang();
        }

        System.Drawing.Font _origBSOD, _origGDI, _origMouse, _origKB,
                             _origBuild, _origLbl2, _origLbl4, _origLbl5,
                             _origAudioName, _origPickVid, _origPickAudio;

        void _StoreFonts()
        {
            if (_origBSOD != null) return;
            _origBSOD      = new System.Drawing.Font(lblSwBSOD.Font,      lblSwBSOD.Font.Style);
            _origGDI       = new System.Drawing.Font(lblSwGDI.Font,       lblSwGDI.Font.Style);
            _origMouse     = new System.Drawing.Font(lblSwMouse.Font,     lblSwMouse.Font.Style);
            _origKB        = new System.Drawing.Font(lblSwKeyboard.Font,  lblSwKeyboard.Font.Style);
            _origBuild     = new System.Drawing.Font(BuildButton1.Font,   BuildButton1.Font.Style);
            _origLbl2      = new System.Drawing.Font(label2.Font,         label2.Font.Style);
            _origLbl4      = new System.Drawing.Font(label4.Font,         label4.Font.Style);
            _origLbl5      = new System.Drawing.Font(label5.Font,         label5.Font.Style);
            _origAudioName = new System.Drawing.Font(lblAudioName.Font,   lblAudioName.Font.Style);
            _origPickVid   = new System.Drawing.Font(guna2Button4.Font,   guna2Button4.Font.Style);
            _origPickAudio = new System.Drawing.Font(btnPickAudio.Font,   btnPickAudio.Font.Style);
        }

        void _ApplyLang()
        {
            var L = new System.Func<string, string>(Localization.T);

            BuildButton1.Text   = L("Build");
            lblSwBSOD.Text      = L("BSOD");
            lblSwGDI.Text       = L("GDI");
            lblSwMouse.Text     = L("Mouse");
            lblSwKeyboard.Text  = L("Keyboard");
            ICONCheckBox1.Text  = L("ICON");
            _btnScr.Text        = L("Screen BG");
            _btnBg.Text         = L("Text BG");
            _btnFg.Text         = L("Text Color");
            guna2Button4.Text   = L("Pick Video");
            btnPickAudio.Text   = L("Pick Audio");

            if (_videoData == null)
                label5.Text = L("No video");
            if (_audioData == null)
                lblAudioName.Text = L("No audio selected");
            if (_imgPixels == null && _imgFrames == null)
                label2.Text = L("Select an image or \r\nGIF to display in MBR");
            if (_screenImgData == null)
                label4.Text = L("Select an image or GIF to \r\ndisplay in full-screen mode");

            MBRTextBox1.PlaceholderText   = L("Enter the text...\r\nto be printed on the MBR...");
            guna2TextBox1.PlaceholderText = L("The text to be printed in GDI");

            _StoreFonts();

            if (Localization.IsArabic)
            {
                Localization.ApplyFont(lblSwBSOD,     8.5f);
                Localization.ApplyFont(lblSwGDI,      8.5f);
                Localization.ApplyFont(lblSwMouse,    8.5f);
                Localization.ApplyFont(lblSwKeyboard, 8.5f);
                Localization.ApplyFont(BuildButton1,  9.5f, System.Drawing.FontStyle.Bold);
                Localization.ApplyFont(label2,        8.5f);
                Localization.ApplyFont(label4,        8.5f);
                Localization.ApplyFont(label5,        8.5f);
                Localization.ApplyFont(lblAudioName,  8.5f);
                Localization.ApplyFont(guna2Button4,  8.5f, System.Drawing.FontStyle.Bold);
                Localization.ApplyFont(btnPickAudio,  8.5f, System.Drawing.FontStyle.Bold);
                lblSwBSOD.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            }
            else
            {
                lblSwBSOD.Font      = _origBSOD;
                lblSwGDI.Font       = _origGDI;
                lblSwMouse.Font     = _origMouse;
                lblSwKeyboard.Font  = _origKB;
                BuildButton1.Font   = _origBuild;
                label2.Font         = _origLbl2;
                label4.Font         = _origLbl4;
                label5.Font         = _origLbl5;
                lblAudioName.Font   = _origAudioName;
                guna2Button4.Font   = _origPickVid;
                btnPickAudio.Font   = _origPickAudio;
                lblSwBSOD.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            }
        }
    }
}
