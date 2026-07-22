namespace AorusLcd.Core;

/// <summary>
/// Legacy GvLcdApi command opcodes recovered from ucVga.dll. Every command
/// frame is [opcode, CB 55 AC 38, params...] zero-padded to 256 bytes.
/// </summary>
public static class Opcode
{
    public static readonly byte[] Magic = [0xCB, 0x55, 0xAC, 0x38];

    public const byte Save = 0xAA;       // persist current LCD config to panel NVRAM
    public const byte GetFwVersion = 0xD6; // read firmware version (array[1] hi.lo nibble)
    public const byte GetMode = 0xDE;    // read current mode (array[1]-1) + on state (array[2])
    public const byte GetDisplay = 0xDF; // read dashboard element bitmask + interval
    public const byte SetDisplay = 0xE1; // dashboard sensor elements + rotation interval
    public const byte SensorFeed = 0xE3; // live sensor values (temp/clock/usage/fan/ram/fps/tgp)
    public const byte SetMode = 0xE5;    // byte5 = mode+1 (mode 7 -> internal 9)
    public const byte OpenLcd = 0xE7;    // byte5: 1 = panel ON, 2 = panel OFF
    public const byte SetImageTpl = 0xEA; // template: type, color, image/data positions, enable
    public const byte GetImageTpl = 0xEB; // read template color/enable (byte5 = type)
    public const byte GetImageTplData = 0xED; // read template data position (second half of GetImageTpl)
    public const byte SetLoop = 0xF3;    // carousel: byte5 = interval, byte6.. = (mode+1) order
    public const byte GetLoop = 0xF4;    // read carousel (byte5 = bank 1..5), 8 bytes each
    public const byte PowerOff = 0xFA;   // SetPCPowerOffMode (experimental)

    public const byte UploadMarker = 0xF2; // BEGIN (flag 1) / END (flag 2)
    public const byte UploadHeader = 0xF1; // 19-byte upload header

    // EB with subcommand 3 reads the PET template; a valid read-back doubles as
    // a presence probe (the same query the tool uses to confirm the controller).
    public const byte Probe = GetImageTpl;
}
