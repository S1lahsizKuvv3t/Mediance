using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Mediance.AcrylicProbe.Windowing;

internal static class WindowPreview
{
    // Renders only this app's XAML in solid mode for layout review. This does
    // not capture the desktop, show a screen-control overlay or move the pointer.
    internal static async Task SaveAsync(UIElement surface, string file)
    {
        var target = new RenderTargetBitmap();
        await target.RenderAsync(surface);
        var buffer = await target.GetPixelsAsync();
        using var reader = DataReader.FromBuffer(buffer);
        var pixels = new byte[buffer.Length];
        reader.ReadBytes(pixels);
        using var memory = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memory);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)target.PixelWidth, (uint)target.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
        memory.Seek(0);
        using var result = new DataReader(memory.GetInputStreamAt(0));
        await result.LoadAsync((uint)memory.Size);
        var png = new byte[memory.Size];
        result.ReadBytes(png);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        await File.WriteAllBytesAsync(file, png);
    }
}
