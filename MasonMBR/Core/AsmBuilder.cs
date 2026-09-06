using System.Collections.Generic;

namespace MasonMBR.Core
{
    static class AsmBuilder
    {
        const string TextAsm =
            "bits 16\norg 0x7C00\n\n" +
            "start:\n" +
            "    cli\n    xor ax, ax\n    mov ds, ax\n    mov es, ax\n" +
            "    mov ss, ax\n    mov sp, 0x7C00\n    sti\n" +
            "    mov ax, 0x0003\n    int 0x10\n" +
            "{SCR_CLEAR}" +
            "    xor ax, ax\n    mov es, ax\n" +
            "    mov ax, 0x1301\n    mov bh, 0x00\n    mov bl, {ATTR}\n" +
            "    mov dh, 0\n    mov dl, 0\n" +
            "    mov cx, {LEN}\n    mov bp, msg\n    int 0x10\n" +
            ".done:\n    hlt\n    jmp .done\n\n" +
            "msg:\n    db {BYTES}\n\n" +
            "times 510-($-$$) db 0\ndw 0xAA55\n";

        const string ImageAsm =
            "bits 16\norg 0x7C00\n\n" +
            "start:\n" +
            "    cli\n    xor ax, ax\n    mov ds, ax\n    mov es, ax\n" +
            "    mov ss, ax\n    mov sp, 0x7C00\n    sti\n" +
            "    mov si, probe_dap\n    mov ah, 0x42\n    mov dl, 0x80\n    int 0x13\n" +
            "    jc .load_img\n" +
            "    mov ax, [0x7000]\n    cmp ax, 0x4645\n    jne .load_img\n" +
            "    mov ax, [0x7002]\n    cmp ax, 0x2049\n    jne .load_img\n" +
            "    mov word [dap + 8], 34\n    mov word [dap + 10], 0\n" +
            ".load_img:\n" +
            "    mov si, dap\n    mov ah, 0x42\n    mov dl, 0x80\n    int 0x13\n" +
            "    mov ax, 0x0013\n    int 0x10\n    cld\n" +
            "    mov ax, 0x0800\n    mov ds, ax\n    mov ax, 0xA000\n    mov es, ax\n" +
            "    xor si, si\n    xor di, di\n    mov cx, 32000\n    rep movsw\n" +
            ".halt:\n    cli\n    hlt\n    jmp .halt\n\n" +
            "probe_dap:\n    db 0x10, 0x00\n    dw 1\n    dw 0x0000\n    dw 0x0700\n    dq 1\n\n" +
            "dap:\n    db 0x10\n    db 0x00\n    dw 125\n    dw 0x0000\n    dw 0x0800\n    dq 1\n\n" +
            "times 510-($-$$) db 0\ndw 0xAA55\n";

        const string AnimatedAsm =
            "bits 16\norg 0x7C00\n\n" +
            "start:\n" +
            "    cli\n    xor ax, ax\n    mov ds, ax\n    mov es, ax\n" +
            "    mov ss, ax\n    mov sp, 0x7C00\n    sti\n" +
            "    mov si, probe_dap\n    mov ah, 0x42\n    mov dl, 0x80\n    int 0x13\n" +
            "    jc .start_vga\n" +
            "    mov ax, [0x7000]\n    cmp ax, 0x4645\n    jne .start_vga\n" +
            "    mov ax, [0x7002]\n    cmp ax, 0x2049\n    jne .start_vga\n" +
            "    mov word [img_base], 34\n" +
            ".start_vga:\n" +
            "    mov ax, 0x0013\n    int 0x10\n" +
            "    xor ax, ax\n    mov ds, ax\n" +
            "    xor bp, bp\n" +
            ".next:\n" +
            "    mov ax, bp\n    mov bx, 125\n    mul bx\n" +
            "    add ax, word [img_base]\n" +
            "    mov word [dap + 8], ax\n    mov word [dap + 10], 0\n" +
            "    mov si, dap\n    mov ah, 0x42\n    mov dl, 0x80\n    int 0x13\n" +
            "    cld\n" +
            "    mov ax, 0x0800\n    mov ds, ax\n    mov ax, 0xA000\n    mov es, ax\n" +
            "    xor si, si\n    xor di, di\n    mov cx, 32000\n    rep movsw\n" +
            "    xor ax, ax\n    mov ds, ax\n" +
            "    mov bx, bp\n    shl bx, 1\n" +
            "    mov ax, word [delay_tbl + bx]\n" +
            "    cmp ax, 5\n    jae .got_delay\n    mov ax, 5\n" +
            ".got_delay:\n" +
            "    mov si, ax\n" +
            "    xor ax, ax\n    int 0x1a\n" +
            "    mov di, dx\n" +
            ".twait:\n" +
            "    xor ax, ax\n    int 0x1a\n" +
            "    mov bx, dx\n    sub bx, di\n" +
            "    cmp bx, si\n    jb .twait\n" +
            ".adv:\n" +
            "    inc bp\n    cmp bp, word [fcount]\n    jb .next\n" +
            "    xor bp, bp\n    jmp .next\n\n" +
            "img_base:   dw 1\n" +
            "fcount:     dw {FRAME_COUNT}\n" +
            "delay_tbl:  dw {DELAY_TABLE}\n\n" +
            "probe_dap:\n    db 0x10, 0x00\n    dw 1\n    dw 0x0000\n    dw 0x0700\n    dq 1\n\n" +
            "dap:\n    db 0x10, 0x00\n    dw 125\n    dw 0x0000, 0x0800\n    dq 0\n\n" +
            "times 510-($-$$) db 0\ndw 0xAA55\n";

        internal static string ForAnimatedImage(int frameCount, int[] delays)
        {
            var parts = new List<string>();
            foreach (int d in delays) parts.Add(d.ToString());
            return AnimatedAsm
                .Replace("{FRAME_COUNT}", frameCount.ToString())
                .Replace("{DELAY_TABLE}", string.Join(", ", parts));
        }

        const int _maxBytes = 450;

        internal static string FromTextStyled(string text, byte attr, byte scrIndex)
        {
            var parts = new List<string>();
            foreach (char c in text)
            {
                if (parts.Count >= _maxBytes) break;
                if      (c == '\n') parts.Add("10");
                else if (c == '\r') parts.Add("13");
                else                parts.Add(((int)c).ToString());
            }

            string scrClear =
                "    mov ax, 0x0600\n    mov bh, 0x" + (scrIndex << 4).ToString("X2") +
                "\n    xor cx, cx\n    mov dx, 0x184F\n    int 0x10\n";

            return TextAsm
                .Replace("{SCR_CLEAR}", scrClear)
                .Replace("{ATTR}",      "0x" + attr.ToString("X2"))
                .Replace("{LEN}",       parts.Count.ToString())
                .Replace("{BYTES}",     parts.Count > 0 ? string.Join(", ", parts) : "0");
        }

        internal static string ForImage()
        {
            return ImageAsm;
        }
    }
}
