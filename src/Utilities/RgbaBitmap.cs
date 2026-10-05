using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using L2Toolkit.Processing.Splash;

namespace L2Toolkit.Utilities;

public static class RgbaBitmap
{
    /// <summary>Copia pixels RGBA não pré-multiplicados para um bitmap exibível pelo Avalonia.</summary>
    public static WriteableBitmap ToBitmap(RgbaImage image)
    {
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96),
            PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var buffer = bitmap.Lock();
        var rowBytes = image.Width * 4;
        for (var row = 0; row < image.Height; row++)
            Marshal.Copy(image.Pixels, row * rowBytes, buffer.Address + row * buffer.RowBytes, rowBytes);
        return bitmap;
    }
}
