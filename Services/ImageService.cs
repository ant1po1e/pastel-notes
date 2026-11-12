using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace PastelNotes.Services;

public static class ImageService
{
    private const int MaxDimension = 1920;
    private const int WebpQuality = 85;
    private const int DisplayMaxDimension = 1100;
    private const int CacheLimit = 16;

    private static readonly Regex Reference = new(@"!\[[^\]]*\]\(images/([^)\s]+)\)", RegexOptions.Compiled);
    private static readonly Dictionary<string, BitmapSource> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> CacheOrder = new();

    public static string ImagesDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PastelNotes", "images");

    public static string Import(string sourcePath)
    {
        Directory.CreateDirectory(ImagesDirectory);

        using var source = SKBitmap.Decode(sourcePath);
        if (source == null) throw new InvalidDataException("This file is not a supported image.");

        SKBitmap? resized = null;
        try
        {
            var bitmap = source;
            int largest = Math.Max(source.Width, source.Height);
            if (largest > MaxDimension)
            {
                double scale = (double)MaxDimension / largest;
                var info = new SKImageInfo(
                    Math.Max(1, (int)Math.Round(source.Width * scale)),
                    Math.Max(1, (int)Math.Round(source.Height * scale)));
                resized = source.Resize(info, SKFilterQuality.High);
                if (resized != null) bitmap = resized;
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
            if (data == null) throw new InvalidDataException("Could not convert the image to WebP.");

            var name = Guid.NewGuid().ToString("N") + ".webp";
            using var stream = File.Create(Path.Combine(ImagesDirectory, name));
            data.SaveTo(stream);
            return name;
        }
        finally
        {
            resized?.Dispose();
        }
    }

    public static BitmapSource? Load(string fileName)
    {
        if (Cache.TryGetValue(fileName, out var cached))
        {
            CacheOrder.Remove(fileName);
            CacheOrder.AddLast(fileName);
            return cached;
        }

        var path = Path.Combine(ImagesDirectory, Path.GetFileName(fileName));
        if (!File.Exists(path)) return null;

        try
        {
            using var source = SKBitmap.Decode(path);
            if (source == null) return null;

            double scale = Math.Min(1.0, (double)DisplayMaxDimension / Math.Max(source.Width, source.Height));
            var info = new SKImageInfo(
                Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale)),
                SKColorType.Bgra8888, SKAlphaType.Premul);

            using var display = ToDisplayBitmap(source, info);
            var bitmap = BitmapSource.Create(
                display.Width, display.Height, 96, 96,
                PixelFormats.Pbgra32, null, display.Bytes, display.RowBytes);
            bitmap.Freeze();

            Remember(fileName, bitmap);
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static SKBitmap ToDisplayBitmap(SKBitmap source, SKImageInfo info)
    {
        var resized = source.Resize(info, SKFilterQuality.Medium);
        if (resized != null) return resized;

        var copy = new SKBitmap(info);
        using var canvas = new SKCanvas(copy);
        canvas.DrawBitmap(source, new SKRect(0, 0, info.Width, info.Height));
        return copy;
    }

    private static void Remember(string fileName, BitmapSource bitmap)
    {
        Cache[fileName] = bitmap;
        CacheOrder.Remove(fileName);
        CacheOrder.AddLast(fileName);

        while (CacheOrder.Count > CacheLimit)
        {
            var oldest = CacheOrder.First!.Value;
            CacheOrder.RemoveFirst();
            Cache.Remove(oldest);
        }
    }

    public static IEnumerable<string> ExtractReferences(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) yield break;
        foreach (Match match in Reference.Matches(markdown))
            yield return Path.GetFileName(match.Groups[1].Value);
    }

    public static void DeleteUnreferenced(ISet<string> referenced)
    {
        if (!Directory.Exists(ImagesDirectory)) return;

        foreach (var file in Directory.EnumerateFiles(ImagesDirectory, "*.webp"))
        {
            var name = Path.GetFileName(file);
            if (referenced.Contains(name)) continue;

            try
            {
                File.Delete(file);
                Cache.Remove(name);
                CacheOrder.Remove(name);
            }
            catch
            {
            }
        }
    }

    public static void CopyTo(string fileName, string targetDirectory)
    {
        var source = Path.Combine(ImagesDirectory, Path.GetFileName(fileName));
        if (!File.Exists(source)) return;
        Directory.CreateDirectory(targetDirectory);
        File.Copy(source, Path.Combine(targetDirectory, Path.GetFileName(fileName)), true);
    }
}
