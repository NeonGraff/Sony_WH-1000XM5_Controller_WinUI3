using System.Buffers.Binary;

namespace SonyHeadphonesNet.Core;

/// <summary>
/// Unpacked MDR Packet representation.
/// </summary>
public record MdrUnpackedPacket(MdrDataType DataType, byte SequenceNumber, byte[] Payload);

/// <summary>
/// MDR Packet framing, byte escaping, checksum and packing/unpacking utilities.
/// </summary>
public static class MdrPacket
{
    public const byte StartMarker = 0x3E; // '>'
    public const byte EndMarker = 0x3C;   // '<'
    public const byte EscapeSentry = 0x3D; // '='

    public const byte Escaped60 = 44; // 0x3C ('<') -> 0x3D 0x2C
    public const byte Escaped61 = 45; // 0x3D ('=') -> 0x3D 0x2D
    public const byte Escaped62 = 46; // 0x3E ('>') -> 0x3D 0x2E

    /// <summary>
    /// Calculates the 8-bit sum checksum over the provided data.
    /// </summary>
    public static byte CalculateChecksum(ReadOnlySpan<byte> data)
    {
        byte sum = 0;
        foreach (byte b in data)
        {
            sum += b;
        }
        return sum;
    }

    /// <summary>
    /// Escapes special bytes (0x3C, 0x3D, 0x3E).
    /// </summary>
    public static byte[] Escape(ReadOnlySpan<byte> data)
    {
        var result = new List<byte>(data.Length * 2);
        foreach (byte b in data)
        {
            switch (b)
            {
                case 0x3C: // '<'
                    result.Add(EscapeSentry);
                    result.Add(Escaped60);
                    break;
                case 0x3D: // '='
                    result.Add(EscapeSentry);
                    result.Add(Escaped61);
                    break;
                case 0x3E: // '>'
                    result.Add(EscapeSentry);
                    result.Add(Escaped62);
                    break;
                default:
                    result.Add(b);
                    break;
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Unescapes escaped bytes.
    /// </summary>
    public static byte[]? Unescape(ReadOnlySpan<byte> data)
    {
        var result = new List<byte>(data.Length);
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (b == EscapeSentry)
            {
                i++;
                if (i >= data.Length)
                    return null; // Malformed

                switch (data[i])
                {
                    case Escaped60:
                        result.Add(0x3C);
                        break;
                    case Escaped61:
                        result.Add(0x3D);
                        break;
                    case Escaped62:
                        result.Add(0x3E);
                        break;
                    default:
                        return null; // Invalid escape
                }
            }
            else
            {
                result.Add(b);
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Packs data into an encoded MDR frame ready to be sent over Bluetooth.
    /// </summary>
    /// <param name="type">Packet data type (e.g. DATA_MDR or ACK)</param>
    /// <param name="seq">Sequence number (0 or 1)</param>
    /// <param name="payload">Payload command bytes</param>
    public static byte[] Pack(MdrDataType type, byte seq, ReadOnlySpan<byte> payload)
    {
        // Unescaped frame layout:
        // [Type (1)][Seq (1)][Length (4, BE)][Payload (N)][Checksum (1)]
        int unescapedLen = 1 + 1 + 4 + payload.Length + 1;
        byte[] unescaped = new byte[unescapedLen];

        unescaped[0] = (byte)type;
        unescaped[1] = seq;
        BinaryPrimitives.WriteInt32BigEndian(unescaped.AsSpan(2, 4), payload.Length);
        payload.CopyTo(unescaped.AsSpan(6, payload.Length));

        // Checksum covers [Type, Seq, Length, Payload]
        byte cs = CalculateChecksum(unescaped.AsSpan(0, 6 + payload.Length));
        unescaped[6 + payload.Length] = cs;

        // Escape
        byte[] escaped = Escape(unescaped);

        // Frame with StartMarker and EndMarker
        byte[] frame = new byte[escaped.Length + 2];
        frame[0] = StartMarker;
        escaped.CopyTo(frame, 1);
        frame[^1] = EndMarker;

        return frame;
    }

    /// <summary>
    /// Creates an ACK packet for a given received sequence number.
    /// In MDR protocol, ACK sequence number is (1 - receivedSeq).
    /// </summary>
    public static byte[] CreateAck(byte receivedSeq)
    {
        byte ackSeq = (byte)(1 - receivedSeq);
        return Pack(MdrDataType.ACK, ackSeq, ReadOnlySpan<byte>.Empty);
    }

    /// <summary>
    /// Attempts to unpack an MDR frame.
    /// </summary>
    public static bool TryUnpack(ReadOnlySpan<byte> frame, out MdrUnpackedPacket? packet)
    {
        packet = null;
        if (frame.Length < 2)
            return false;

        if (frame[0] != StartMarker || frame[^1] != EndMarker)
            return false;

        // Strip start/end markers
        var escapedContent = frame.Slice(1, frame.Length - 2);
        byte[]? unescaped = Unescape(escapedContent);
        if (unescaped == null || unescaped.Length < 7) // Type(1) + Seq(1) + Size(4) + CS(1)
            return false;

        MdrDataType type = (MdrDataType)unescaped[0];
        byte seq = unescaped[1];
        int dataSize = BinaryPrimitives.ReadInt32BigEndian(unescaped.AsSpan(2, 4));

        if (unescaped.Length != 6 + dataSize + 1)
            return false;

        byte checksum = unescaped[^1];
        byte expectedCs = CalculateChecksum(unescaped.AsSpan(0, unescaped.Length - 1));
        if (checksum != expectedCs)
            return false; // Checksum error

        byte[] payload = new byte[dataSize];
        Array.Copy(unescaped, 6, payload, 0, dataSize);

        packet = new MdrUnpackedPacket(type, seq, payload);
        return true;
    }
}
