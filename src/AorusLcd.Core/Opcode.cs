namespace AorusLcd.Core;

/// <summary>
/// Legacy GvLcdApi command opcodes recovered from ucVga.dll. Every command
/// frame is [opcode, CB 55 AC 38, params...] zero-padded to 256 bytes.
/// </summary>
public static class Opcode
{
    public static readonly byte[] Magic = [0xCB, 0x55, 0xAC, 0x38];

    public const byte OpenLcd = 0xE7;   // byte5: 1 = panel ON, 2 = panel OFF
    public const byte SetMode = 0xE5;   // byte5 = mode+1 (0..6 confirmed; 7 -> 9)
    public const byte SetDisplay = 0xE1; // dashboard sensor elements + rotation interval (E1 SetDisplay)
    public const byte SetLoop = 0xF3;   // carousel: byte5 = arg, byte6.. = (mode+1) order
    public const byte PowerOff = 0xFA;  // SetPCPowerOffMode (experimental)
    public const byte TextEffect = 0xAA; // applied after a text upload (rainbow)
    public const byte GetData = 0xEB;   // status query (EB 03): write frame, read 8 back

    public const byte UploadMarker = 0xF2; // BEGIN (flag 1) / END (flag 2)
    public const byte UploadHeader = 0xF1; // 19-byte upload header
}
