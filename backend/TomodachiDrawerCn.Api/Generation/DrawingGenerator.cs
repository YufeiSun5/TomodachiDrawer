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
        request.ReportProgress(10, "正在读取图片。");
        await using var imageStream = File.OpenRead(request.SourceImagePath);
        using var image = SKBitmap.Decode(imageStream)
            ?? throw new InvalidDataException("The uploaded file could not be decoded as an image.");

        request.ReportProgress(15, "正在按裁切框准备 256x256 画布。");
        using var preparedImage = PrepareCanvasImage(image, request);
        SavePreparedPreview(preparedImage, request.PreviewPath);

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
        var drawer = new CanvasDrawer(
            timingSink,
            request.SwitchVersion,
            message => request.ReportProgress(ProgressFromMessage(message), message)
        );
        request.ReportProgress(20, "正在初始化绘画控制器。");
        drawer.ConnectAndConfirmController();
        request.ReportProgress(30, "正在规划绘画路线。");
        await drawer.DrawImage(preparedImage.Copy(), drawSettings);

        request.ReportProgress(95, "正在写入 TDLD 文件。");
        using (var fileSink = new FileControllerSink(tdldPath))
        {
            timingSink.ReplayTo(fileSink);
        }

        var tdldBytes = await File.ReadAllBytesAsync(tdldPath);
        ValidateTdld(tdldBytes);

        if (request.OutputType == "uf2")
        {
            request.ReportProgress(97, "正在打包 UF2 文件。");
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

    private static int ProgressFromMessage(string message)
    {
        if (message.Contains("Quant", StringComparison.OrdinalIgnoreCase))
        {
            return 35;
        }

        if (message.Contains("Detecting", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Scanning", StringComparison.OrdinalIgnoreCase))
        {
            return 45;
        }

        if (message.Contains("[", StringComparison.OrdinalIgnoreCase)
            || message.Contains("TSP", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (message.Contains("bucket", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Done routing", StringComparison.OrdinalIgnoreCase))
        {
            return 88;
        }

        return 55;
    }

    private static SKBitmap PrepareCanvasImage(SKBitmap source, GenerateDrawingRequest request)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new InvalidDataException("The uploaded image has invalid dimensions.");
        }

        var fallbackSize = Math.Min(source.Width, source.Height);
        var requestedSize = request.CropSize.GetValueOrDefault(fallbackSize);
        if (!double.IsFinite(requestedSize) || requestedSize <= 0)
        {
            requestedSize = fallbackSize;
        }

        var cropSize = (float)Math.Clamp(requestedSize, 1, fallbackSize);
        var cropX = (float)Math.Clamp(request.CropX.GetValueOrDefault((source.Width - cropSize) / 2.0), 0, source.Width - cropSize);
        var cropY = (float)Math.Clamp(request.CropY.GetValueOrDefault((source.Height - cropSize) / 2.0), 0, source.Height - cropSize);

        var prepared = new SKBitmap(CanvasDrawer.CanvasWidth, CanvasDrawer.CanvasHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(prepared);
        canvas.Clear(SKColors.White);
        var sourceRect = SKRect.Create(cropX, cropY, cropSize, cropSize);
        var destinationRect = SKRect.Create(0, 0, CanvasDrawer.CanvasWidth, CanvasDrawer.CanvasHeight);
        canvas.DrawBitmap(source, sourceRect, destinationRect);
        canvas.Flush();

        return prepared;
    }

    private static void SavePreparedPreview(SKBitmap preparedImage, string previewPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
        using var image = SKImage.FromBitmap(preparedImage);
        using var data = image.Encode(SKEncodedImageFormat.Png, 95)
            ?? throw new InvalidDataException("Prepared preview image could not be encoded.");
        using var stream = File.Create(previewPath);
        data.SaveTo(stream);
    }

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
    SwitchVersion SwitchVersion,
    string PreviewPath,
    double? CropX,
    double? CropY,
    double? CropSize,
    Action<GeneratedDrawingProgress>? ProgressReporter = null
)
{
    public void ReportProgress(int percent, string message)
    {
        ProgressReporter?.Invoke(new GeneratedDrawingProgress(Math.Clamp(percent, 0, 100), message));
    }
}

internal sealed record GeneratedDrawing(
    string OutputPath,
    string OutputType,
    int TdldBytes,
    int OutputBytes,
    TimeSpan EstimatedDrawTime
);

internal sealed record GeneratedDrawingProgress(int Percent, string Message);
