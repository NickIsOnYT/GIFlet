# GIFlet - the offline alternative to EZGIF

A Windows desktop application for editing GIF images, built with WPF and ImageSharp.

I'm not a programmer so this was fully made using [OpenCode](https://opencode.ai/download) (MiniMax M2.5 Free)

## Features

- **Create GIF** - Make GIFs from multiple images
- **Split GIF** - Extract individual frames from a GIF
- **Combine GIFs** - Merge multiple GIFs together
- **Reverse GIF** - Play GIF backwards
- **Speed** - Adjust playback speed (0.25x to 4x)
- **Optimize** - Reduce file size with color quantization
- **Crop** - Trim GIF to specific dimensions
- **Resize** - Change GIF dimensions (will stretch to fit)
- **Rotate/Flip** - Rotate 90°/180°/270° or flip horizontally/vertically
- **Effects** - Apply Blur, Sharpen, Gaussian Blur, Invert, Grayscale, Sepia
- **Colors** - Adjust Brightness, Contrast, and Saturation
- **Transparency** - Adjust opacity (broken but funny)
- **Add Border** - Add borders around GIF
- **GIF Debug** - View detailed information about GIF files (frames, delays, dimensions, etc.)
- **Video to GIF** (and vice-versa)
- **Sprite sheet to GIF** (and vice-versa)
- **Images to sprite sheet**

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

## The name

- GIFlet is a word play of the word Applet (a small program designed to do one task in a widget engine). I decided to use this name because of the "EZGif alternative" branding (EZGif being an online-only application, and this being an offline alternative.)

## Preview

![GIFlet2.5-ImageMagick](https://github.com/NickIsOnYT/GIFlet/blob/main/Logo/Screenshot%20GIFlet%2025.png)
