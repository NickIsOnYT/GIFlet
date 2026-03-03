using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats;
using System.Collections.ObjectModel;
using SysPath = System.IO.Path;
using ImageSharpPoint = SixLabors.ImageSharp.Point;
using ImageSharpRect = SixLabors.ImageSharp.Rectangle;

namespace GIFlet;

public class NaturalStringComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        int ix = 0, iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                var numX = "";
                var numY = "";
                while (ix < x.Length && char.IsDigit(x[ix])) { numX += x[ix]; ix++; }
                while (iy < y.Length && char.IsDigit(y[iy])) { numY += y[iy]; iy++; }
                var cmp = int.Parse(numX).CompareTo(int.Parse(numY));
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = x[ix].CompareTo(y[iy]);
                if (cmp != 0) return cmp;
                ix++; iy++;
            }
        }
        return x.Length.CompareTo(y.Length);
    }
}

public partial class MainWindow : Window
{
    private string? _currentGifPath;
    private readonly ObservableCollection<string> _createImages = new();
    private readonly ObservableCollection<string> _combineGifs = new();

    public MainWindow()
    {
        InitializeComponent();
        CreateImageList.ItemsSource = _createImages;
        CombineGifList.ItemsSource = _combineGifs;
    }

    private void ShowSaveDialog(Action<string> saveAction)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "GIF Image|*.gif",
            DefaultExt = "gif"
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                saveAction(dialog.FileName);
                MessageBox.Show("Operation completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ShowOpenDialog(string filter, Action<string> openAction)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                openAction(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private BitmapImage LoadBitmapImage(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private byte[] LoadGifBytes(string path) => File.ReadAllBytes(path);

    private static GifFrameMetadata GetGifFrameMetadata(ImageFrame frame) => frame.Metadata.GetGifMetadata();

    // Create GIF
    private void CreateAddImages_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                if (!_createImages.Contains(file))
                    _createImages.Add(file);
            }
            var sorted = _createImages.OrderBy(f => f, new NaturalStringComparer()).ToList();
            _createImages.Clear();
            foreach (var item in sorted) _createImages.Add(item);
        }
    }

    private void CreateClearImages_Click(object sender, RoutedEventArgs e)
    {
        _createImages.Clear();
    }

    private async void CreateGif_Click(object sender, RoutedEventArgs e)
    {
        if (_createImages.Count == 0)
        {
            MessageBox.Show("Please add at least one image.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ShowSaveDialog(async (path) =>
        {
            var delay = int.Parse(CreateFrameDelay.Text);
            var loop = CreateLoopCount.SelectedIndex == 0 ? 0 : CreateLoopCount.SelectedIndex;

            var frames = new List<Image<Rgba32>>();

            foreach (var imgPath in _createImages)
            {
                var img = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(imgPath);
                frames.Add(img);
            }

            if (frames.Count > 0)
            {
                var width = frames[0].Width;
                var height = frames[0].Height;

                using var output = new Image<Rgba32>(width, height);
                var gifMeta = output.Metadata.GetGifMetadata();
                gifMeta.RepeatCount = (ushort)loop;

                foreach (var frameImg in frames)
                {
                    using var resized = frameImg.Clone(ctx => ctx.Resize(width, height));
                    GetGifFrameMetadata(resized.Frames.RootFrame).FrameDelay = delay / 10;
                    output.Frames.AddFrame(resized.Frames.RootFrame);
                }

                output.Frames.RemoveFrame(0);

                await output.SaveAsGifAsync(path, new GifEncoder
                {
                    ColorTableMode = GifColorTableMode.Local
                });

                foreach (var f in frames) f.Dispose();
            }
        });
    }

    // Split GIF
    private void SplitOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            SplitOriginalImage.SourcePath = path;
        });
    }

    private async void SplitExtractFrames_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);
            var frameDir = SysPath.Combine(SysPath.GetDirectoryName(path)!, "frames");
            Directory.CreateDirectory(frameDir);

            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var framePath = SysPath.Combine(frameDir, $"frame_{i:D4}.png");
                using var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                await frameImg.SaveAsPngAsync(framePath);
            }

            var framesList = new ObservableCollection<BitmapImage>();
            foreach (var f in Directory.GetFiles(frameDir).OrderBy(f => f, new NaturalStringComparer()))
            {
                framesList.Add(LoadBitmapImage(f));
            }
            SplitFramesList.ItemsSource = framesList;
        });
    }

    // Combine GIFs
    private void CombineAddGifs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "GIF Image|*.gif",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                _combineGifs.Add(file);
            }
        }
    }

    private void CombineMoveUp_Click(object sender, RoutedEventArgs e)
    {
        var selected = CombineGifList.SelectedIndex;
        if (selected > 0)
        {
            var item = _combineGifs[selected];
            _combineGifs.RemoveAt(selected);
            _combineGifs.Insert(selected - 1, item);
        }
    }

    private void CombineMoveDown_Click(object sender, RoutedEventArgs e)
    {
        var selected = CombineGifList.SelectedIndex;
        if (selected >= 0 && selected < _combineGifs.Count - 1)
        {
            var item = _combineGifs[selected];
            _combineGifs.RemoveAt(selected);
            _combineGifs.Insert(selected + 1, item);
        }
    }

    private void CombineRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = CombineGifList.SelectedIndex;
        if (selected >= 0)
        {
            _combineGifs.RemoveAt(selected);
        }
    }

    private async void CombineGifs_Click(object sender, RoutedEventArgs e)
    {
        if (_combineGifs.Count == 0)
        {
            MessageBox.Show("Please add at least one GIF.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ShowSaveDialog(async (path) =>
        {
            var allFrames = new List<Image<Rgba32>>();

            foreach (var gifPath in _combineGifs)
            {
                using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(gifPath);
                var delay = GetGifFrameMetadata(gif.Frames.RootFrame).FrameDelay;
                var gifWidth = gif.Width;
                var gifHeight = gif.Height;

                for (int i = 0; i < gif.Frames.Count; i++)
                {
                    var frameImg = new Image<Rgba32>(gifWidth, gifHeight);
                    frameImg.Frames.AddFrame(gif.Frames[i]);
                    frameImg.Frames.RemoveFrame(0);
                    GetGifFrameMetadata(frameImg.Frames.RootFrame).FrameDelay = delay;
                    allFrames.Add(frameImg);
                }
            }

            if (allFrames.Count > 0)
            {
                var width = allFrames[0].Width;
                var height = allFrames[0].Height;

                using var output = new Image<Rgba32>(width, height);
                var gifMeta = output.Metadata.GetGifMetadata();
                gifMeta.RepeatCount = 0;

                foreach (var frame in allFrames)
                {
                    output.Frames.AddFrame(frame.Frames.RootFrame);
                }

                output.Frames.RemoveFrame(0);

                await output.SaveAsGifAsync(path, new GifEncoder
                {
                    ColorTableMode = GifColorTableMode.Local
                });

                foreach (var f in allFrames) f.Dispose();
            }
        });
    }

    // Reverse GIF
    private void ReverseOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            ReversePreviewImage.SourcePath = path;
        });
    }

    private async void ReverseGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);
            var frameCount = gif.Frames.Count;
            var frames = new Image<Rgba32>[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                frames[i] = frameImg;
            }

            using var output = new Image<Rgba32>(gif.Width, gif.Height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            for (int i = frameCount - 1; i >= 0; i--)
            {
                GetGifFrameMetadata(frames[i].Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                output.Frames.AddFrame(frames[i].Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            for (int i = 0; i < frameCount; i++) frames[i].Dispose();
        });
    }

    // Speed
    private void SpeedOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            SpeedPreviewImage.SourcePath = path;
        });
    }

    private async void SpeedApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var multipliers = new[] { 0.25f, 0.5f, 1f, 2f, 3f, 4f };
        var multiplier = multipliers[SpeedMultiplier.SelectedIndex];

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var delay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                GetGifFrameMetadata(frameImg.Frames.RootFrame).FrameDelay = (int)(delay / multiplier);
                frames.Add(frameImg);
            }

            using var output = new Image<Rgba32>(gif.Width, gif.Height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Optimize
    private void OptimizeOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            OptimizePreviewImage.SourcePath = path;
        });
    }

    private async void OptimizeGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var processedFrames = new List<Image<Rgba32>>();
            var maxColors = OptimizeLevel.SelectedIndex switch
            {
                0 => 224,
                1 => 192,
                2 => 128,
                3 => 64,
                _ => 256
            };
            var doQuantize = maxColors < 256;

            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                Image<Rgba32> processed;
                if (doQuantize)
                {
                    var quantizer = new OctreeQuantizer(new QuantizerOptions { MaxColors = maxColors });
                    processed = frameImg.Clone(ctx => ctx.Quantize(quantizer));
                }
                else
                {
                    processed = frameImg;
                }
                
                var delay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                GetGifFrameMetadata(processed.Frames.RootFrame).FrameDelay = delay;
                
                processedFrames.Add(processed);
                if (doQuantize) frameImg.Dispose();
            }

            if (processedFrames.Count > 0)
            {
                using var output = new Image<Rgba32>(gif.Width, gif.Height);
                var gifMeta = output.Metadata.GetGifMetadata();
                gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

                foreach (var frame in processedFrames)
                {
                    output.Frames.AddFrame(frame.Frames.RootFrame);
                }

                output.Frames.RemoveFrame(0);
                await output.SaveAsGifAsync(path);

                foreach (var f in processedFrames) f.Dispose();
            }
        });
    }

    // Crop
    private async void CropOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", async (path) =>
        {
            _currentGifPath = path;
            CropPreviewImage.SourcePath = path;
            
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(path);
            CropWidth.Text = gif.Width.ToString();
            CropHeight.Text = gif.Height.ToString();
        });
    }

    private async void CropGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var width = int.Parse(CropWidth.Text);
        var height = int.Parse(CropHeight.Text);

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                var cropped = frameImg.Clone(ctx => ctx.Crop(new ImageSharpRect(0, 0, width, height)));
                GetGifFrameMetadata(cropped.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(cropped);
                frameImg.Dispose();
            }

            using var output = new Image<Rgba32>(width, height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Resize
    private void ResizeOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            ResizePreviewImage.SourcePath = path;
        });
    }

    private async void ResizeGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var width = int.Parse(ResizeWidth.Text);
        var height = int.Parse(ResizeHeight.Text);

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                var resized = frameImg.Clone(ctx => ctx.Resize(width, height));
                GetGifFrameMetadata(resized.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(resized);
                frameImg.Dispose();
            }

            using var output = new Image<Rgba32>(width, height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Rotate
    private void RotateOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            RotatePreviewImage.SourcePath = path;
        });
    }

    private async void RotateApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var angles = new[] { 0f, 90f, 180f, 270f };
        var angle = angles[RotateAngle.SelectedIndex];
        var flipMode = FlipDirection.SelectedIndex switch
        {
            1 => FlipMode.Horizontal,
            2 => FlipMode.Vertical,
            _ => (FlipMode?)null
        };

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var newWidth = gif.Width;
            var newHeight = gif.Height;
            if (angle == 90 || angle == 270)
            {
                newWidth = gif.Height;
                newHeight = gif.Width;
            }

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                if (angle > 0)
                    frameImg.Mutate(ctx => ctx.Rotate(angle));
                if (flipMode.HasValue)
                    frameImg.Mutate(ctx => ctx.Flip(flipMode.Value));

                GetGifFrameMetadata(frameImg.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(frameImg);
            }

            using var output = new Image<Rgba32>(newWidth, newHeight);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Effects
    private void EffectsOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            EffectsPreviewImage.SourcePath = path;
        });
    }

    private async void EffectsApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var effectIndex = EffectType.SelectedIndex;
        var intensity = (float)(EffectIntensity.Value / 100f);

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                switch (effectIndex)
                {
                    case 0: // Blur
                        frameImg.Mutate(ctx => ctx.GaussianBlur(intensity * 20f));
                        break;
                    case 1: // Sharpen
                        frameImg.Mutate(ctx => ctx.GaussianSharpen(intensity * 5f));
                        break;
                    case 2: // Gaussian Blur
                        frameImg.Mutate(ctx => ctx.GaussianBlur(intensity * 30f));
                        break;
                    case 3: // Invert
                        frameImg.Mutate(ctx => ctx.Invert());
                        break;
                    case 4: // Grayscale
                        frameImg.Mutate(ctx => ctx.Grayscale());
                        break;
                    case 5: // Sepia
                        frameImg.Mutate(ctx => ctx.Sepia());
                        break;
                }
                GetGifFrameMetadata(frameImg.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(frameImg);
            }

            using var output = new Image<Rgba32>(gif.Width, gif.Height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Colors
    private void ColorOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            ColorPreviewImage.SourcePath = path;
        });
    }

    private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ColorBrightnessVal == null || ColorContrastVal == null || ColorSaturationVal == null) return;
        
        var bValue = (int)(ColorBrightness.Value - 100);
        var cValue = (int)(ColorContrast.Value - 100);
        var sValue = (int)(ColorSaturation.Value - 100);
        
        ColorBrightnessVal.Text = bValue.ToString();
        ColorContrastVal.Text = cValue.ToString();
        ColorSaturationVal.Text = sValue.ToString();
    }

    private async void ColorApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var brightness = (float)(ColorBrightness.Value / 100f);
        var contrast = (float)(ColorContrast.Value / 100f);
        var saturation = (float)(ColorSaturation.Value / 100f);

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                frameImg.Mutate(ctx =>
                {
                    if (brightness != 0)
                        ctx.Brightness(brightness);
                    if (contrast != 1f)
                        ctx.Contrast(contrast);
                    if (saturation != 1f)
                        ctx.Saturate(saturation);
                });
                GetGifFrameMetadata(frameImg.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(frameImg);
            }

            using var output = new Image<Rgba32>(gif.Width, gif.Height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Transparency
    private void TransparencyOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            TransparencyPreviewImage.SourcePath = path;
        });
    }

    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TransparencyAmountVal == null) return;
        TransparencyAmountVal.Text = $"{(int)TransparencyAmount.Value}%";
    }

    private async void TransparencyApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var transparency = (float)(TransparencyAmount.Value / 100f);

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                frameImg.Mutate(ctx => ctx.Opacity(1f - transparency));
                GetGifFrameMetadata(frameImg.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(frameImg);
            }

            using var output = new Image<Rgba32>(gif.Width, gif.Height);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // Border
    private void BorderOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            BorderPreviewImage.SourcePath = path;
        });
    }

    private async void BorderApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var borderWidth = int.Parse(BorderWidth.Text);
        var colorName = new[] { "Black", "White", "Red", "Blue", "Green" }[BorderColor.SelectedIndex];

        var borderColor = colorName switch
        {
            "White" => SixLabors.ImageSharp.Color.White,
            "Red" => SixLabors.ImageSharp.Color.Red,
            "Blue" => SixLabors.ImageSharp.Color.Blue,
            "Green" => SixLabors.ImageSharp.Color.Green,
            _ => SixLabors.ImageSharp.Color.Black
        };

        ShowSaveDialog(async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(_currentGifPath);

            var newWidth = gif.Width + (borderWidth * 2);
            var newHeight = gif.Height + (borderWidth * 2);

            var frames = new List<Image<Rgba32>>();
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frameImg = new Image<Rgba32>(gif.Width, gif.Height);
                frameImg.Frames.AddFrame(gif.Frames[i]);
                frameImg.Frames.RemoveFrame(0);
                
                var bordered = new Image<Rgba32>(newWidth, newHeight, borderColor);
                bordered.Mutate(ctx => ctx.DrawImage(frameImg, new ImageSharpPoint(borderWidth, borderWidth), 1f));
                GetGifFrameMetadata(bordered.Frames.RootFrame).FrameDelay = GetGifFrameMetadata(gif.Frames[i]).FrameDelay;
                frames.Add(bordered);
                frameImg.Dispose();
            }

            using var output = new Image<Rgba32>(newWidth, newHeight);
            var gifMeta = output.Metadata.GetGifMetadata();
            gifMeta.RepeatCount = gif.Metadata.GetGifMetadata().RepeatCount;

            foreach (var frame in frames)
            {
                output.Frames.AddFrame(frame.Frames.RootFrame);
            }

            output.Frames.RemoveFrame(0);
            await output.SaveAsGifAsync(path);

            foreach (var f in frames) f.Dispose();
        });
    }

    // GIF Debug
    private async void DebugOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", async (path) =>
        {
            using var gif = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(path);
            
            var fileInfo = new FileInfo(path);
            var fileSize = fileInfo.Length;
            var gifMeta = gif.Metadata.GetGifMetadata();
            
            var totalDelay = 0;
            foreach (var frame in gif.Frames)
            {
                totalDelay += frame.Metadata.GetGifMetadata().FrameDelay;
            }
            var lengthSeconds = totalDelay / 100.0;
            
            var output = new System.Text.StringBuilder();
            output.AppendLine("=== GIF Debug Info ===");
            output.AppendLine();
            output.AppendLine($"GIF size: {fileSize:N0} bytes ({fileSize / 1024.0 / 1024.0:F2} MB)");
            output.AppendLine($"GIF length: {lengthSeconds:F1} second(s)");
            output.AppendLine($"GIF width/height: {gif.Width}×{gif.Height} pixels");
            output.AppendLine($"number of frames: {gif.Frames.Count}");
            output.AppendLine($"number of colors: 256");
            output.AppendLine($"loop count: {gifMeta.RepeatCount} {(gifMeta.RepeatCount == 0 ? "(endless)" : "")}");
            output.AppendLine();
            
            for (int i = 0; i < gif.Frames.Count; i++)
            {
                var frame = gif.Frames[i];
                var frameMeta = frame.Metadata.GetGifMetadata();
                
                output.AppendLine($"Frame #{i + 1}:");
                output.AppendLine("---------");
                output.AppendLine($"x: 0");
                output.AppendLine($"y: 0");
                output.AppendLine($"width: {gif.Width}");
                output.AppendLine($"height: {gif.Height}");
                output.AppendLine($"delay: {frameMeta.FrameDelay * 10}ms");
                output.AppendLine($"disposal: {(int)frameMeta.DisposalMethod}");
                output.AppendLine();
            }
            
            DebugOutput.Text = output.ToString();
        });
    }
}
