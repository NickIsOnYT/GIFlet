# GIFlet - the offline alternative to EZGIF

A Windows desktop application for editing GIF images, built with WPF and ImageSharp.

I'm not a programmer so this was fully made using [OpenCode](https://opencode.ai/download) (MiniMax M2.5 Free)

If you want more controls when making a GIF, you can visit my other project: [Mif](https://github.com/NickIsOnYT/Mif)

## Features

- **Create GIF** - Make GIFs from multiple images
- **Split GIF** - Extract individual frames from a GIF
- **Combine GIFs** - Merge multiple GIFs together
- **Reverse GIF** - Play GIF backwards
- **Speed** - Adjust playback speed (0.25x to 4x)
- **Optimize** - Reduce file size with color quantization
- **Crop** - Trim GIF to specific dimensions
- **Resize** - Change GIF dimensions
- **Rotate/Flip** - Rotate 90°/180°/270° or flip horizontally/vertically
- **Effects** - Apply Blur, Sharpen, Gaussian Blur, Invert, Grayscale, Sepia
- **Colors** - Adjust Brightness, Contrast, and Saturation
- **Transparency** - Adjust opacity
- **Add Border** - Add colored borders around GIF
- **GIF Debug** - View detailed information about GIF files (frames, delays, dimensions, etc.)
- **Video to GIF** - MP4 to GIF file
- **GIF to video** - GIF to MP4 file
- **Sprite sheet to GIF** - PNG Sprite sheet to GIF with proper alignment

## Requirements

- Windows 10 or later
- [.NET 10.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- probably [FFMPEG](https://ffmpeg.org/download.html)

## Building

```bash
dotnet build
dotnet run
```

Or run the compiled executable from `bin/Debug/net10.0-windows/GIFlet.exe`

## Preview

![GIFlet2.1](https://github.com/NickIsOnYT/GIFlet/blob/main/Logo/Screenshot%20GIFlet.png)
