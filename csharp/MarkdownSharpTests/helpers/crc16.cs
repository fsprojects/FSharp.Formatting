/// <summary>
/// http://www.sanity-free.org/134/standard_crc_16_in_csharp.html
/// </summary>
public class Crc16
{
    const ushort polynomial = 0xA001;
    ushort[] table = new ushort[256];

    public ushort ComputeChecksum(byte[] bytes)
    {
        ushort crc = 0;
        for (int i = 0; i < bytes.Length; ++i)
        {
            byte index = (byte)(crc ^ bytes[i]);
            crc = (ushort)((crc >> 8) ^ table[index]);
        }
        return crc;
    }

    public byte[] ComputeChecksumBytes(byte[] bytes)
    {
        ushort crc = ComputeChecksum(bytes);
        return new byte[] { (byte)(crc >> 8), (byte)(crc & 0x00ff) };
    }

    public Crc16()
    {
        ushort value;
        ushort temp;
        for (ushort i = 0; i < table.Length; ++i)
        {
            value = 0;
            temp = i;
            for (byte j = 0; j < 8; ++j)
            {
                if (((value ^ temp) & 0x0001) != 0)
                {
                    value = (ushort)((value >> 1) ^ polynomial);
                }
                else
                {
                    value >>= 1;
                }
                temp >>= 1;
            }
            table[i] = value;
        }
    }
}