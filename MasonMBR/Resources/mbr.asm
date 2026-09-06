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

    mov ax, 0x0003
    int 0x10

    mov ah, 0x02
    mov bh, 0x00
    mov dh, 12
    mov dl, 20
    int 0x10

    mov si, message
    mov ah, 0x0E
    mov bh, 0x00
print_loop:
    lodsb
    cmp al, 0
    je halt
    int 0x10
    jmp print_loop

halt:
    hlt
    jmp halt

message:
    db {BYTES}, 0

times 510-($-$$) db 0
dw 0xAA55