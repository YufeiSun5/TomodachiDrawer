using SkiaSharp;
using TomodachiDrawer.Core;
using TomodachiDrawer.Core.ImageProcessing.Quantizers;
using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawerCn.Api.Generation;

internal static class DrawingGenerator
{
    public static async Task<GeneratedDrawing> GenerateAsync(GenerateDrawingRequest request)
    {
        await using var imageStream = File.OpenRead(request.SourceImagePath);
        using var image = SKBitmap.Decode(imageStream)
            ?? throw new InvalidDataException("The uploaded file could not be decoded as an image.");

        if (image.Width > CanvasDrawer.CanvasWidth || image.Height > CanvasDrawer.CanvasHeight)
        {
            throw new InvalidDataException(
                $"Image too big. Max is {CanvasDrawer.CanvasWidth}x{CanvasDrawer.CanvasHeight}."
            );
        }

        var tdldPath = Path.ChangeExtension(request.OutputPath, ".tdld");
        Directory.CreateDirectory(Path.GetDirectoryName(tdldPath)!);

        var drawSettings = new DrawImageSettings
        {
            QuantizerSettings = BuildQuantizerSettings(request.ColourMatcher),
            TSPTimeLimit = Math.Clamp(request.TspTimeLimit, 1, 120),
            DisableLargeBrush = false,
            EnableExperimentalFeatures = false,
            HomeToTopLeft = true,
            ReverseColourOrder = false,
        };

        var timingSink = new TimingSink();
        var drawer = new CanvasDrawer(timingSink, request.SwitchVersion, _ => { });
        drawer.ConnectAndConfirmController();
        await drawer.DrawImage(image.Copy(), drawSettings);

        using (var fileSink = new FileControllerSink(tdldPath))
        {
            timingSink.ReplayTo(fileSink);
        }

        var tdldBytes = await File.ReadAllBytesAsync(tdldPath);
        ValidateTdld(tdldBytes);

        if (request.OutputType == "uf2")
        {
            var chip = request.BoardType == "rp2350" ? RpChipType.Rp2350 : RpChipType.Rp2040;
            var uf2Bytes = Uf2Builder.BuildTdldUf2(tdldBytes, chip);
            ValidateUf2(uf2Bytes, chip);
            await File.WriteAllBytesAsync(request.OutputPath, uf2Bytes);
            return new GeneratedDrawing(request.OutputPath, "uf2", tdldBytes.Length, uf2Bytes.Length, timingSink.TotalTime);
        }

        if (!StringComparer.OrdinalIgnoreCase.Equals(tdldPath, request.OutputPath))
        {
            File.Copy(tdldPath, request.OutputPath, overwrite: true);
        }

        return new GeneratedDrawing(request.OutputPath, "tdld", tdldBytes.Length, tdldBytes.Length, timingSink.TotalTime);
    }

    public static bool IsUf2Supported(string boardType) =>
        boardType is "rp2040" or "rp2350";

    private static QuantizerSettings BuildQuantizerSettings(string colourMatcher)
    {
        var name = colourMatcher.ToLowerInvariant() switch
        {
            "cielab" => "CieLab",
            "redmean" => "Redmean",
            "euclidean" => "Euclidean",
            _ => "Arbitrary",
        };

        return name == "Arbitrary"
            ? new QuantizerSettings(name, ColourCount: 32)
            : new QuantizerSettings(name);
    }

    private static void ValidateTdld(byte[] bytes)
    {
        if (bytes.Length < 7 || bytes[0] != 'T' || bytes[1] != 'D' || bytes[2] != 'L' || bytes[3] != 'D')
        {
            throw new InvalidDataException("Generated TDLD is missing the TDLD magic header.");
        }

        if (bytes[4] != 3)
        {
            throw new InvalidDataException($"Unsupported TDLD version byte: {bytes[4]}.");
        }

        if (bytes[^1] != 0x00)
        {
            throw new InvalidDataException("Generated TDLD is missing the terminating invalid opcode.");
        }
    }

    private static void ValidateUf2(byte[] bytes, RpChipType chip)
    {
        if (bytes.Length == 0 || bytes.Length % 512 != 0)
        {
            throw new InvalidDataException("Generated UF2 size must be a positive multiple of 512 bytes.");
        }

        var block = bytes.AsSpan(0, 512);
        var familyId = BitConverter.ToUInt32(block[0x01C..0x020]);
        var expectedFamilyId = chip == RpChipType.Rp2350 ? 0xE48BFF57u : 0xE48BFF56u;
        if (
            BitConverter.ToUInt32(block[0x000..0x004]) != 0x0A324655u
            || BitConverter.ToUInt32(block[0x004..0x008]) != 0x9E5D5157u
            || BitConverter.ToUInt32(block[0x1FC..0x200]) != 0x0AB16F30u
            || familyId != expectedFamilyId
        )
        {
            throw new InvalidDataException("Generated UF2 failed magic/family validation.");
        }
    }
}

internal sealed record GenerateDrawingRequest(
    string SourceImagePath,
    string OutputPath,
    string BoardType,
    string OutputType,
    string ColourMatcher,
    int TspTimeLimit,
    SwitchVersion SwitchVersion
);

internal sealed record GeneratedDrawing(
    string OutputPath,
    string OutputType,
    int TdldBytes,
    int OutputBytes,
    TimeSpan EstimatedDrawTime
);
