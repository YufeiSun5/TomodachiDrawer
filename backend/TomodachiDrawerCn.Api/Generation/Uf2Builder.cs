using System.Buffers.Binary;

namespace TomodachiDrawerCn.Api.Generation;

internal enum RpChipType
{
    Rp2040,
    Rp2350,
}

internal static class Uf2Builder
{
    public static byte[] BuildTdldUf2(byte[] tdldData, RpChipType chip)
    {
        const int maxTdldSize = 1 * 1024 * 1024;
        if (tdldData.Length > maxTdldSize)
        {
            throw new ArgumentException(
                $"TDLD data exceeds maximum size of {maxTdldSize} bytes.",
                nameof(tdldData)
            );
        }

        const uint targetBase = 0x10100000u;
        const uint payloadSize = 256u;
        var familyId = chip == RpChipType.Rp2350 ? 0xE48BFF57u : 0xE48BFF56u;
        var blockCount = (tdldData.Length + (int)payloadSize - 1) / (int)payloadSize;
        var output = new byte[blockCount * 512];

        for (var i = 0; i < blockCount; i++)
        {
            var block = output.AsSpan(i * 512, 512);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x000..], 0x0A324655);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x004..], 0x9E5D5157);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x008..], 0x00002000);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x00C..], targetBase + (uint)(i * payloadSize));
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x010..], payloadSize);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x014..], (uint)i);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x018..], (uint)blockCount);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x01C..], familyId);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x1FC..], 0x0AB16F30);

            var srcOffset = i * (int)payloadSize;
            var copyLength = Math.Min((int)payloadSize, tdldData.Length - srcOffset);
            tdldData.AsSpan(srcOffset, copyLength).CopyTo(block[0x020..]);
        }

        return output;
    }
}
