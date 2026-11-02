using ImageMagick;
using System.IO;

namespace GIFlet;

internal sealed class RasterImage : IDisposable
{
    private readonly List<RasterFrame> _frames;
    private readonly RasterFrameCollection _frameCollection;

    private RasterImage(IEnumerable<MagickImage> frames, uint repeatCount)
    {
        _frames = frames.Select(image => new RasterFrame(image)).ToList();
        if (_frames.Count == 0)
            throw new InvalidDataException("The image contains no frames.");

        _frameCollection = new RasterFrameCollection(_frames);
        Metadata = new RasterImageMetadata(repeatCount);
    }

    public RasterImage(int width, int height)
        : this([new MagickImage(MagickColors.Transparent, (uint)width, (uint)height)], 0)
    {
    }

    public RasterImage(int width, int height, RasterColor color)
        : this([new MagickImage(new MagickColor(color.R, color.G, color.B, color.A), (uint)width, (uint)height)], 0)
    {
    }

    public int Width => Frames.RootFrame.Width;
    public int Height => Frames.RootFrame.Height;
    public RasterFrameCollection Frames => _frameCollection;
    public RasterImageMetadata Metadata { get; }
    public Rgba32 this[int x, int y]
    {
        get => Frames.RootFrame[x, y];
        set => Frames.RootFrame[x, y] = value;
    }

    public static RasterImage Load<TPixel>(string path)
    {
        using var source = new MagickImageCollection();
        source.Read(path);
        source.Coalesce();

        var frames = source.Select(frame =>
        {
            var clone = frame.Clone();
            clone.Alpha(AlphaAction.Set);
            return (MagickImage)clone;
        });

        return new RasterImage(frames, source.Count == 0 ? 0 : source[0].AnimationIterations);
    }

    public static Task<RasterImage> LoadAsync<TPixel>(string path) =>
        Task.Run(() => Load<TPixel>(path));

    public RasterImage Clone()
    {
        return new RasterImage(
            _frames.Select(frame => (MagickImage)frame.Image.Clone()),
            Metadata.GetGifMetadata().RepeatCount);
    }

    public RasterImage Clone(Action<RasterImageProcessingContext> mutate)
    {
        var clone = Clone();
        clone.Mutate(mutate);
        return clone;
    }

    public void Mutate(Action<RasterImageProcessingContext> mutate) =>
        mutate(new RasterImageProcessingContext(Frames.RootFrame));

    public async Task SaveAsGifAsync(string path, GifEncoder? encoder = null)
    {
        await Task.Run(() => SaveAsGif(path, encoder));
    }

    public void SaveAsGif(string path) => SaveAsGif(path, null);

    private void SaveAsGif(string path, GifEncoder? encoder)
    {
        using var output = new MagickImageCollection();
        foreach (var frame in _frames)
        {
            var clone = frame.Image.Clone();
            clone.AnimationIterations = Metadata.GetGifMetadata().RepeatCount;
            clone.AnimationTicksPerSecond = 100;
            clone.Page = new MagickGeometry(0, 0, clone.Width, clone.Height);
            if (encoder?.ColorTableMode == GifColorTableMode.Local)
                clone.Quantize(new QuantizeSettings { Colors = 256 });
            output.Add(clone);
        }

        output.Write(path, MagickFormat.Gif);
    }

    public void SaveAsPng(string path) => Frames.RootFrame.Image.Write(path, MagickFormat.Png);
    public void SaveAsPng(Stream stream) => Frames.RootFrame.Image.Write(stream, MagickFormat.Png);

    public async Task SaveAsPngAsync(string path) =>
        await Task.Run(() => SaveAsPng(path));

    public async Task SaveAsPngAsync(Stream stream) =>
        await Task.Run(() => Frames.RootFrame.Image.Write(stream, MagickFormat.Png));

    public void Save(string path) => SaveAsPng(path);

    public void Dispose()
    {
        foreach (var frame in _frames)
            frame.Dispose();
    }
}

internal sealed class RasterFrame : IDisposable
{
    private readonly RasterFrameMetadata _metadata;

    internal RasterFrame(MagickImage image)
    {
        Image = image;
        Image.Alpha(AlphaAction.Set);
        _metadata = new RasterFrameMetadata(image);
        Metadata = new RasterFrameMetadataProvider(_metadata);
    }

    internal MagickImage Image { get; }
    public int Width => (int)Image.Width;
    public int Height => (int)Image.Height;
    public RasterFrameMetadataProvider Metadata { get; }

    public Rgba32 this[int x, int y]
    {
        get
        {
            var color = new MagickColor(Image.GetPixels().GetPixel(x, y).ToColor()!);
            return new Rgba32(color.R, color.G, color.B, color.A);
        }
        set => Image.GetPixels().SetPixel(x, y, [value.R, value.G, value.B, value.A]);
    }

    public RasterFrame Clone() => new((MagickImage)Image.Clone());

    public void Dispose() => Image.Dispose();
}

internal sealed class RasterFrameCollection : IEnumerable<RasterFrame>
{
    private readonly List<RasterFrame> _frames;

    internal RasterFrameCollection(List<RasterFrame> frames) => _frames = frames;

    public int Count => _frames.Count;
    public RasterFrame RootFrame => _frames[0];
    public RasterFrame this[int index] => _frames[index];

    public void AddFrame(RasterFrame frame) => _frames.Add(frame.Clone());

    public void RemoveFrame(int index)
    {
        _frames[index].Dispose();
        _frames.RemoveAt(index);
    }

    public IEnumerator<RasterFrame> GetEnumerator() => _frames.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class RasterImageMetadata(uint repeatCount)
{
    private uint _repeatCount = repeatCount;
    public RasterGifMetadata GetGifMetadata() => new(this);
    internal uint RepeatCount { get => _repeatCount; set => _repeatCount = value; }
}

internal sealed class RasterFrameMetadataProvider(RasterFrameMetadata metadata)
{
    public RasterFrameMetadata GetGifMetadata() => metadata;
}

internal sealed class RasterFrameMetadata(MagickImage image)
{
    public int FrameDelay
    {
        get => (int)image.AnimationDelay;
        set => image.AnimationDelay = (uint)Math.Max(value, 0);
    }

    public int DisposalMethod { get; set; }
}

internal sealed class RasterGifMetadata(RasterImageMetadata metadata)
{
    public uint RepeatCount
    {
        get => metadata.RepeatCount;
        set => metadata.RepeatCount = value;
    }
}

internal sealed class RasterImageProcessingContext(RasterFrame frame)
{
    public void Resize(int width, int height) =>
        frame.Image.Resize(new MagickGeometry((uint)width, (uint)height) { IgnoreAspectRatio = true });

    public void Crop(RasterRectangle rectangle) =>
        frame.Image.Crop(new MagickGeometry(rectangle.X, rectangle.Y, (uint)rectangle.Width, (uint)rectangle.Height));

    public void DrawImage(RasterImage image, float opacity) =>
        DrawImage(image, 0, 0, opacity);

    public void DrawImage(RasterImage image, RasterPoint point, float opacity) =>
        DrawImage(image, point.X, point.Y, opacity);

    private void DrawImage(RasterImage image, int x, int y, float opacity)
    {
        var source = image.Frames.RootFrame.Image;
        if (opacity < 1)
        {
            using var adjusted = (MagickImage)source.Clone();
            adjusted.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, new Percentage(Math.Clamp(opacity, 0, 1) * 100));
            frame.Image.Composite(adjusted, x, y, CompositeOperator.Over);
            return;
        }

        frame.Image.Composite(source, x, y, CompositeOperator.Over);
    }

    public void GaussianBlur(float sigma) => frame.Image.GaussianBlur(0, sigma);
    public void GaussianSharpen(float sigma) => frame.Image.Sharpen(0, sigma);
    public void Invert() => frame.Image.Negate();
    public void Grayscale() => frame.Image.Grayscale();
    public void Sepia() => frame.Image.SepiaTone(new Percentage(80));

    public void Rotate(float angle) => frame.Image.Rotate(angle);

    public void Flip(RasterFlipMode mode)
    {
        if (mode == RasterFlipMode.Horizontal)
            frame.Image.Flop();
        else
            frame.Image.Flip();
    }

    public void Brightness(float value) =>
        frame.Image.BrightnessContrast(new Percentage((value - 1) * 100), new Percentage(0));

    public void Contrast(float value) =>
        frame.Image.BrightnessContrast(new Percentage(0), new Percentage((value - 1) * 100));

    public void Saturate(float value) =>
        frame.Image.Modulate(new Percentage(100), new Percentage(value * 100), new Percentage(100));

    public void Opacity(float value)
    {
        frame.Image.Alpha(AlphaAction.Set);
        frame.Image.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, new Percentage(value * 100));
    }

    public void Quantize(OctreeQuantizer quantizer) =>
        frame.Image.Quantize(new QuantizeSettings { Colors = (uint)quantizer.MaxColors });
}

internal sealed class OctreeQuantizer(int maxColors)
{
    public int MaxColors => maxColors;
}

internal sealed class GifEncoder
{
    public GifColorTableMode ColorTableMode { get; set; }
}
internal enum GifColorTableMode { Local }
internal enum RasterFlipMode { Horizontal, Vertical }
internal readonly record struct RasterRectangle(int X, int Y, int Width, int Height);
internal readonly record struct RasterPoint(int X, int Y);
internal record struct Rgba32
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }

    public Rgba32(byte r, byte g, byte b, byte a) =>
        (R, G, B, A) = (r, g, b, a);
}

internal readonly record struct RasterColor(byte R, byte G, byte B, byte A)
{
    public static RasterColor Transparent => new(0, 0, 0, 0);
    public static RasterColor Black => new(0, 0, 0, 255);
    public static RasterColor White => new(255, 255, 255, 255);
    public static RasterColor Red => new(255, 0, 0, 255);
    public static RasterColor Blue => new(0, 0, 255, 255);
    public static RasterColor Green => new(0, 128, 0, 255);
}
