bits 16
org 0x7C00

start:
    cli
    xor ax, ax
    mov ds, ax
    mov es, ax
    mov ss, ax
    mov sp, 0x7C00
    sti

    mov si, dap
    mov ah, 0x42
    mov dl, 0x80
    int 0x13

    mov ax, 0x0013
    int 0x10

    cld
    mov ax, 0x0800
    mov ds, ax
    mov ax, 0xA000
    mov es, ax
    xor si, si
    xor di, di
    mov cx, 32000
    rep movsw

.halt:
    cli
    hlt
    jmp .halt

dap:
    db 0x10
    db 0x00
    dw 125
    dw 0x0000
    dw 0x0800
    dq 1

times 510-($-$$) db 0
dw 0xAA55
