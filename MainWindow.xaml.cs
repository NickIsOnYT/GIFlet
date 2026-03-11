using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ImageMagick;
using System.Collections.ObjectModel;
using SysPath = System.IO.Path;

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
        PngToSpriteImageList.ItemsSource = _pngToSpriteImages;
        MainTabControl.SelectedIndex = 0;
        LoadRandomTitle();
    }

    private void LoadRandomTitle()
    {
        try
        {
            var json = File.ReadAllText("phrases.json");
            var phrases = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
            if (phrases != null && phrases.Count > 0)
            {
                var random = new Random();
                TitleText.Text = phrases[random.Next(phrases.Count)];
            }
        }
        catch { }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        ModeSelector.SelectedIndex = 19;
    }

    private void ModeSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (MainTabControl == null) return;
        MainTabControl.SelectedIndex = ModeSelector.SelectedIndex;
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

    private void ShowOpenDialogMultiple(string filter, Action<string[]> openAction)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                openAction(dialog.FileNames);
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

    private static MagickImage GetFrame(MagickImage img, int index) => img;

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

    private void CreateFrameDelay_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CreateFpsDisplay == null) return;
        if (int.TryParse(CreateFrameDelay.Text, out var ms) && ms > 0)
        {
            var fps = 1000.0 / ms;
            CreateFpsDisplay.Text = $"{fps:F1} fps";
        }
        else
        {
            CreateFpsDisplay.Text = "— fps";
        }
    }

    private void SpriteFrameDelay_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SpriteFpsDisplay == null) return;
        if (int.TryParse(SpriteFrameDelay.Text, out var ms) && ms > 0)
        {
            var fps = 1000.0 / ms;
            SpriteFpsDisplay.Text = $"{fps:F1} fps";
        }
        else
        {
            SpriteFpsDisplay.Text = "— fps";
        }
    }

    private void CreateImageList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
    }

    private void CreateMoveUp_Click(object sender, RoutedEventArgs e)
    {
        var selected = CreateImageList.SelectedItems.Cast<string>().ToList();
        if (selected.Count == 0) return;
        var newIndices = new List<int>();
        foreach (var item in selected)
        {
            var idx = _createImages.IndexOf(item);
            if (idx > 0)
            {
                _createImages.RemoveAt(idx);
                _createImages.Insert(idx - 1, item);
                newIndices.Add(idx - 1);
            }
            else
            {
                newIndices.Add(idx);
            }
        }
        CreateImageList.SelectionChanged -= CreateImageList_SelectionChanged;
        CreateImageList.SelectedItem = null;
        foreach (var idx in newIndices)
        {
            CreateImageList.SelectedItems.Add(_createImages[idx]);
        }
        CreateImageList.SelectionChanged += CreateImageList_SelectionChanged;
    }

    private void CreateMoveDown_Click(object sender, RoutedEventArgs e)
    {
        var selected = CreateImageList.SelectedItems.Cast<string>().ToList();
        if (selected.Count == 0) return;
        var newIndices = new List<int>();
        for (int i = selected.Count - 1; i >= 0; i--)
        {
            var item = selected[i];
            var idx = _createImages.IndexOf(item);
            if (idx < _createImages.Count - 1)
            {
                _createImages.RemoveAt(idx);
                _createImages.Insert(idx + 1, item);
                newIndices.Insert(0, idx + 1);
            }
            else
            {
                newIndices.Insert(0, idx);
            }
        }
        CreateImageList.SelectionChanged -= CreateImageList_SelectionChanged;
        CreateImageList.SelectedItem = null;
        foreach (var idx in newIndices)
        {
            CreateImageList.SelectedItems.Add(_createImages[idx]);
        }
        CreateImageList.SelectionChanged += CreateImageList_SelectionChanged;
    }

    private void CreateRemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = CreateImageList.SelectedItems.Cast<string>().ToList();
        foreach (var item in selected)
        {
            _createImages.Remove(item);
        }
    }

    private void RefreshCreateImageList()
    {
        var temp = CreateImageList.SelectedItems.Cast<string>().ToList();
        CreateImageList.ItemsSource = null;
        CreateImageList.ItemsSource = _createImages;
        foreach (var item in temp)
        {
            CreateImageList.SelectedItems.Add(item);
        }
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
            var loop = int.TryParse(CreateLoopCount.Text, out var loopVal) ? loopVal : 0;
            var noStack = CreateNoStackFrames.IsChecked == true;
            var firstFrameBg = CreateFirstFrameBg.IsChecked == true;

            var frames = new List<MagickImage>();

            foreach (var imgPath in _createImages)
            {
                var img = new MagickImage(imgPath);
                frames.Add(img);
            }

            if (frames.Count > 0)
            {
                var width = frames[0].Width;
                var height = frames[0].Height;

                var gif = new MagickImageCollection();

                int startIdx = (firstFrameBg && noStack) ? 1 : 0;
                for (int i = startIdx; i < frames.Count; i++)
                {
                    using var resized = new MagickImage(frames[i]);
                    resized.Resize(width, height);
                    MagickImage frameToAdd;
                    if (firstFrameBg && noStack)
                    {
                        using var composite = new MagickImage(MagickColors.Transparent, width, height);
                        composite.Composite(frames[0], CompositeOperator.Copy);
                        composite.Composite(resized, CompositeOperator.Over);
                        frameToAdd = new MagickImage(composite);
                    }
                    else if (noStack || firstFrameBg)
                    {
                        using var fullFrame = new MagickImage(MagickColors.Transparent, width, height);
                        fullFrame.Composite(resized, CompositeOperator.Copy);
                        frameToAdd = new MagickImage(fullFrame);
                    }
                    else
                    {
                        frameToAdd = new MagickImage(resized);
                    }
                    frameToAdd.AnimationDelay = (ushort)(delay / 10);
                    gif.Add(frameToAdd);
                }

                gif[0].AnimationIterations = (ushort)loop;
                await gif.WriteAsync(path);

                foreach (var f in frames) f.Dispose();
            }
        });
    }

    private async void CreateMp4_Click(object sender, RoutedEventArgs e)
    {
        if (_createImages.Count == 0)
        {
            MessageBox.Show("Please add at least one image.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "MP4 Video|*.mp4",
            DefaultExt = "mp4"
        };
        if (dialog.ShowDialog() != true) return;

        var delay = int.Parse(CreateFrameDelay.Text);
        if (delay == 0) delay = 100;
        var noStack = CreateNoStackFrames.IsChecked == true;
        var firstFrameBg = CreateFirstFrameBg.IsChecked == true;

        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"giflet_mp4_{Guid.NewGuid()}");
        System.IO.Directory.CreateDirectory(tempDir);

        var frames = new List<MagickImage>();

        foreach (var imgPath in _createImages)
        {
            var img = new MagickImage(imgPath);
            frames.Add(img);
        }

        if (frames.Count > 0)
        {
            var width = frames[0].Width;
            var height = frames[0].Height;

            int startIdx = (firstFrameBg && noStack) ? 1 : 0;
            int frameNum = 0;
            for (int i = startIdx; i < frames.Count; i++)
            {
                using var resized = new MagickImage(frames[i]);
                resized.Resize(width, height);
                MagickImage frameToSave;
                if (firstFrameBg && noStack)
                {
                    using var composite = new MagickImage(MagickColors.Transparent, width, height);
                    composite.Composite(frames[0], CompositeOperator.Copy);
                    composite.Composite(resized, CompositeOperator.Over);
                    frameToSave = new MagickImage(composite);
                }
                else if (noStack || firstFrameBg)
                {
                    using var fullFrame = new MagickImage(MagickColors.Transparent, width, height);
                    fullFrame.Composite(resized, CompositeOperator.Copy);
                    frameToSave = new MagickImage(fullFrame);
                }
                else
                {
                    frameToSave = new MagickImage(resized);
                }

                var framePath = System.IO.Path.Combine(tempDir, $"frame_{frameNum:D5}.png");
                frameToSave.Write(framePath, MagickFormat.Png);
                frameToSave.Dispose();
                frameNum++;
            }

            foreach (var f in frames) f.Dispose();

            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                System.IO.Directory.Delete(tempDir, true);
                MessageBox.Show("FFmpeg not found. Please install FFmpeg and ensure it's in your PATH for MP4 export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var fps = 1000.0 / delay;
            var fpsStr = fps.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -framerate {fpsStr} -i \"{tempDir}\\frame_%05d.png\" -c:v libx264 -pix_fmt yuv420p \"{dialog.FileName}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = tempDir
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            await process.WaitForExitAsync();
            System.IO.Directory.Delete(tempDir, true);
            if (process.ExitCode == 0)
            {
                MessageBox.Show("MP4 created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                MessageBox.Show($"FFmpeg error: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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

    private void SplitClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        SplitOriginalImage.Source = null;
        SplitFramesList.ItemsSource = null;
    }

    private async void SplitExtractFrames_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);
            var frameDir = SysPath.Combine(SysPath.GetDirectoryName(path)!, "frames");
            Directory.CreateDirectory(frameDir);

            for (int i = 0; i < gif.Count; i++)
            {
                var framePath = SysPath.Combine(frameDir, $"frame_{i:D4}.png");
                gif[i].Write(framePath, MagickFormat.Png);
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
            var allFrames = new List<MagickImage>();

            var output = new MagickImageCollection();

            foreach (var gifPath in _combineGifs)
            {
                using var gif = new MagickImageCollection(gifPath);
                foreach (var frame in gif)
                {
                    var frameImg = new MagickImage(frame);
                    frameImg.AnimationDelay = frame.AnimationDelay;
                    output.Add(frameImg);
                }
            }

            if (output.Count > 0)
            {
                output[0].AnimationIterations = 0;
                await output.WriteAsync(path, MagickFormat.Gif);
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

    private void ReverseClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        ReversePreviewImage.Source = null;
    }

    private async void ReverseGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);
            
            var output = new MagickImageCollection();
            for (int i = gif.Count - 1; i >= 0; i--)
            {
                var frameImg = new MagickImage(gif[i]);
                frameImg.AnimationDelay = gif[i].AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);
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

    private void SpeedClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        SpeedPreviewImage.Source = null;
    }

    private async void SpeedApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var multipliers = new[] { 0.25f, 0.5f, 1f, 2f, 3f, 4f };
        var multiplier = multipliers[SpeedMultiplier.SelectedIndex];

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);
            
            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                frameImg.AnimationDelay = (ushort)(frame.AnimationDelay / multiplier);
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);
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

    private void OptimizeClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        OptimizePreviewImage.Source = null;
    }

    private async void OptimizeGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            var maxColors = OptimizeLevel.SelectedIndex switch
            {
                0 => 256,
                1 => 192,
                2 => 128,
                3 => 64,
                _ => 256
            };

            var quantizeSettings = new QuantizeSettings
            {
                Colors = (uint)maxColors,
                DitherMethod = DitherMethod.FloydSteinberg
            };

            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                if (maxColors < 256)
                {
                    frameImg.Quantize(quantizeSettings);
                }
                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    // Crop
    private async void CropOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", async (path) =>
        {
            _currentGifPath = path;
            CropPreviewImage.SourcePath = path;
            
            using var gif = new MagickImage(path);
            CropWidth.Text = gif.Width.ToString();
            CropHeight.Text = gif.Height.ToString();
        });
    }

    private void CropClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        CropPreviewImage.Source = null;
        CropWidth.Text = "0";
        CropHeight.Text = "0";
    }

    private async void CropGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var x = int.Parse(CropX.Text);
        var y = int.Parse(CropY.Text);
        var width = int.Parse(CropWidth.Text);
        var height = int.Parse(CropHeight.Text);

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                frameImg.Crop(new MagickGeometry(x, y, (uint)width, (uint)height));
                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);
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

    private void ResizeClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        ResizePreviewImage.Source = null;
    }

    private async void ResizeGif_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var width = int.Parse(ResizeWidth.Text);
        var height = int.Parse(ResizeHeight.Text);

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                frameImg.Resize((uint)width, (uint)height);
                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void RotateClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        RotatePreviewImage.Source = null;
    }

    private async void RotateApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var angles = new[] { 0f, 90f, 180f, 270f };
        var angle = angles[RotateAngle.SelectedIndex];
        var flipDir = FlipDirection.SelectedIndex;

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                
                if (angle > 0)
                    frameImg.Rotate(angle);
                if (flipDir == 1)
                    frameImg.Flip();
                if (flipDir == 2)
                    frameImg.Flop();

                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);
        });
    }

    private void EffectsOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", (path) =>
        {
            _currentGifPath = path;
            EffectsPreviewImage.SourcePath = path;
        });
    }

    private void EffectsClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        EffectsPreviewImage.Source = null;
    }

    private async void EffectsApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var effectIndex = EffectType.SelectedIndex;
        var intensity = (float)(EffectIntensity.Value / 100f);

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                
                switch (effectIndex)
                {
                    case 0: // Blur
                        frameImg.GaussianBlur(intensity * 20f);
                        break;
                    case 1: // Sharpen
                        frameImg.Sharpen();
                        break;
                    case 2: // Gaussian Blur
                        frameImg.GaussianBlur(intensity * 30f);
                        break;
                    case 3: // Invert
                        frameImg.Negate();
                        break;
                    case 4: // Grayscale
                        frameImg.Grayscale();
                        break;
                    case 5: // Sepia
                        frameImg.Format = MagickFormat.Png;
                        break;
                }
                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void ColorClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        ColorPreviewImage.Source = null;
        ColorBrightness.Value = 100;
        ColorContrast.Value = 100;
        ColorSaturation.Value = 100;
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
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                
                if (brightness != 0)
                    frameImg.Evaluate(Channels.RGB, EvaluateOperator.Multiply, brightness);
                if (contrast != 1f)
                    frameImg.Evaluate(Channels.RGB, EvaluateOperator.Multiply, contrast);
                if (saturation != 1f)
                    frameImg.Modulate(new Percentage(100 * saturation));
                    
                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void TransparencyClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        TransparencyPreviewImage.Source = null;
        TransparencyAmount.Value = 0;
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
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                frameImg.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, 1f - transparency);
                frameImg.AnimationDelay = frame.AnimationDelay;
                output.Add(frameImg);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void BorderClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        BorderPreviewImage.Source = null;
        BorderWidth.Text = "10";
        BorderColor.SelectedIndex = 0;
    }

    private async void BorderApply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var borderWidth = int.Parse(BorderWidth.Text);
        var colorName = new[] { "Black", "White", "Red", "Blue", "Green" }[BorderColor.SelectedIndex];

        var borderColor = colorName switch
        {
            "White" => MagickColors.White,
            "Red" => MagickColors.Red,
            "Blue" => MagickColors.Blue,
            "Green" => MagickColors.Green,
            _ => MagickColors.Black
        };

        ShowSaveDialog(async (path) =>
        {
            using var gif = new MagickImageCollection(_currentGifPath);

            var output = new MagickImageCollection();
            foreach (var frame in gif)
            {
                var frameImg = new MagickImage(frame);
                var bordered = new MagickImage(borderColor, (uint)(frameImg.Width + borderWidth * 2), (uint)(frameImg.Height + borderWidth * 2));
                bordered.Composite(frameImg, borderWidth, borderWidth, CompositeOperator.Over);
                bordered.AnimationDelay = frame.AnimationDelay;
                output.Add(bordered);
            }

            output[0].AnimationIterations = gif[0].AnimationIterations;
            await output.WriteAsync(path, MagickFormat.Gif);

            MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    // GIF Debug
    private async void DebugOpenGif_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", async (path) =>
        {
            using var gif = new MagickImageCollection(path);
            
            var fileInfo = new FileInfo(path);
            var fileSize = fileInfo.Length;
            
            var totalDelay = 0;
            foreach (var frame in gif)
            {
                totalDelay += (int)frame.AnimationDelay;
            }
            var lengthSeconds = totalDelay / 100.0;
            
            var output = new System.Text.StringBuilder();
            output.AppendLine("=== GIF Debug Info ===");
            output.AppendLine();
            output.AppendLine($"GIF size: {fileSize:N0} bytes ({fileSize / 1024.0 / 1024.0:F2} MB)");
            output.AppendLine($"GIF length: {lengthSeconds:F1} second(s)");
            output.AppendLine($"GIF width/height: {gif[0].Width}×{gif[0].Height} pixels");
            output.AppendLine($"number of frames: {gif.Count}");
            output.AppendLine($"number of colors: 256");
            output.AppendLine($"loop count: {gif[0].AnimationIterations}");
            output.AppendLine();
            
            for (int i = 0; i < gif.Count; i++)
            {
                var frame = gif[i];
                
                output.AppendLine($"Frame #{i + 1}:");
                output.AppendLine("---------");
                output.AppendLine($"x: 0");
                output.AppendLine($"y: 0");
                output.AppendLine($"width: {gif[0].Width}");
                output.AppendLine($"height: {gif[0].Height}");
                output.AppendLine($"delay: {frame.AnimationDelay * 10}ms");
                output.AppendLine();
            }
            
            DebugOutput.Text = output.ToString();
        });
    }

    private void DebugClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        DebugOutput.Text = "";
    }

    private string? _currentVideoPath;

    private void VideoToGifOpen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.avi;*.mov;*.mkv;*.wmv;*.webm|All Files|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            _currentVideoPath = dialog.FileName;
            try
            {
                VideoPreview.Source = new Uri(_currentVideoPath);
                VideoPreview.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading video: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void VideoToGifClear_Click(object sender, RoutedEventArgs e)
    {
        _currentVideoPath = null;
        VideoPreview.Stop();
        VideoPreview.Source = null;
    }

    private async void VideoToGifConvert_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentVideoPath)) return;

        var dialog = new SaveFileDialog
        {
            Filter = "GIF Image|*.gif",
            DefaultExt = "gif"
        };
        if (dialog.ShowDialog() != true) return;

        VideoPreview.Stop();

        try
        {
            var startTime = double.Parse(VideoStartTime.Text);
            var duration = double.Parse(VideoDuration.Text);
            var fps = int.Parse(VideoFps.Text);
            var width = int.Parse(VideoWidth.Text);

            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "giflet_video_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            var args = $"-y -ss {startTime} -t {duration} -i \"{_currentVideoPath}\" -vf \"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" -loop 0 \"{tempDir}\\output.gif\"";
            
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                MessageBox.Show("FFmpeg not found. Please install FFmpeg and ensure it's in your PATH.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Directory.Delete(tempDir, true);
                return;
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (System.IO.File.Exists(System.IO.Path.Combine(tempDir, "output.gif")))
            {
                System.IO.File.Copy(System.IO.Path.Combine(tempDir, "output.gif"), dialog.FileName, true);
                MessageBox.Show("GIF created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                MessageBox.Show($"Error creating GIF: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Directory.Delete(tempDir, true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? FindFfmpeg()
    {
        var paths = new[]
        {
            "ffmpeg",
            "ffmpeg.exe",
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"
        };

        foreach (var path in paths)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(3000);
                if (proc?.ExitCode == 0) return path;
            }
            catch { }
        }
        return null;
    }

    private void GifToVideoOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", path =>
        {
            _currentGifPath = path;
            GifToVideoPreview.SourcePath = path;
        });
    }

    private void GifToVideoClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifPath = null;
        GifToVideoPreview.SourcePath = null;
    }

    private async void GifToVideoExport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentGifPath)) return;

        var formats = new[] { "mp4", "webm", "avi" };
        var format = formats[GifToVideoFormat.SelectedIndex];
        var qualities = new[] { "28", "35", "50" };
        var quality = qualities[GifToVideoQuality.SelectedIndex];

        var dialog = new SaveFileDialog
        {
            Filter = $"{format.ToUpper()} Video|*.{format}",
            DefaultExt = format
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                MessageBox.Show("FFmpeg not found. Please install FFmpeg and ensure it's in your PATH.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var args = $"-y -i \"{_currentGifPath}\" -c:v libx264 -crf {quality} -pix_fmt yuv420p \"{dialog.FileName}\"";
            
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (System.IO.File.Exists(dialog.FileName))
            {
                MessageBox.Show("Video exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                MessageBox.Show($"Error exporting video: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? _currentSpriteSheetPath;

    private void SpriteToGifOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("Image Files|*.png;*.jpg;*.jpeg;*.bmp", path =>
        {
            _currentSpriteSheetPath = path;
            var bitmap = new BitmapImage(new Uri(path));
            SpriteToGifPreview.Source = bitmap;
            
            SpriteFrameList.Items.Clear();
            var columns = int.Parse(SpriteColumns.Text);
            var rows = int.Parse(SpriteRows.Text);
            var totalFrames = columns * rows;
            for (int i = 0; i < totalFrames; i++)
            {
                SpriteFrameList.Items.Add(new FrameItem { FrameNumber = i, IsSelected = true });
            }
        });
    }

    private void SpriteToGifClear_Click(object sender, RoutedEventArgs e)
    {
        _currentSpriteSheetPath = null;
        SpriteToGifPreview.Source = null;
        SpriteFrameList.Items.Clear();
    }

    private void SpriteColumns_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSpriteFrameList();
    }

    private void SpriteRows_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSpriteFrameList();
    }

    private void UpdateSpriteFrameList()
    {
        if (_currentSpriteSheetPath == null) return;

        if (!int.TryParse(SpriteColumns.Text, out int columns)) columns = 4;
        if (!int.TryParse(SpriteRows.Text, out int rows)) rows = 4;

        var totalFrames = columns * rows;
        var existingCount = SpriteFrameList.Items.Count;

        if (totalFrames > existingCount)
        {
            for (int i = existingCount; i < totalFrames; i++)
            {
                SpriteFrameList.Items.Add(new FrameItem { FrameNumber = i, IsSelected = true });
            }
        }
        else if (totalFrames < existingCount)
        {
            while (SpriteFrameList.Items.Count > totalFrames)
            {
                SpriteFrameList.Items.RemoveAt(SpriteFrameList.Items.Count - 1);
            }
        }
    }

    public class FrameItem : System.ComponentModel.INotifyPropertyChanged
    {
        public int FrameNumber { get; set; }
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public override string ToString() => $"Frame {FrameNumber}";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    private async void SpriteToGifCreate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentSpriteSheetPath)) return;

        var selectedFrames = SpriteFrameList.Items.Cast<FrameItem>().Where(f => f.IsSelected).Select(f => f.FrameNumber).ToList();
        if (selectedFrames.Count == 0)
        {
            MessageBox.Show("Please select at least one frame.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ShowSaveDialog(async savePath =>
        {
            var columns = int.Parse(SpriteColumns.Text);
            var rows = int.Parse(SpriteRows.Text);
            var frameWidth = int.Parse(SpriteFrameWidth.Text);
            var frameHeight = int.Parse(SpriteFrameHeight.Text);

            using var image = new MagickImage(_currentSpriteSheetPath);
            
            if (frameWidth == 0) frameWidth = (int)(image.Width / columns);
            if (frameHeight == 0) frameHeight = (int)(image.Height / rows);

            var frameDelay = int.Parse(SpriteFrameDelay.Text);
            if (frameDelay == 0) frameDelay = 100;

            var output = new MagickImageCollection();
            
            foreach (var frameNum in selectedFrames)
            {
                var x = (frameNum % columns) * frameWidth;
                var y = (frameNum / columns) * frameHeight;
                
                var cropped = new MagickImage(image);
                cropped.Crop(new MagickGeometry(x, y, (uint)frameWidth, (uint)frameHeight));
                cropped.AnimationDelay = (ushort)(frameDelay / 10);
                output.Add(cropped);
            }

            await output.WriteAsync(savePath, MagickFormat.Gif);
        });
    }

    private async void SpriteToGifExportFrames_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentSpriteSheetPath)) return;

        var columns = int.Parse(SpriteColumns.Text);
        var rows = int.Parse(SpriteRows.Text);
        var frameWidth = int.Parse(SpriteFrameWidth.Text);
        var frameHeight = int.Parse(SpriteFrameHeight.Text);

        using var image = new MagickImage(_currentSpriteSheetPath);
        
        if (frameWidth == 0) frameWidth = (int)(image.Width / columns);
        if (frameHeight == 0) frameHeight = (int)(image.Height / rows);

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            DefaultExt = "png",
            FileName = "frame_0000.png"
        };
        if (dialog.ShowDialog() != true) return;

        var folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrEmpty(folderPath)) return;

        var count = 0;
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                using var frame = new MagickImage(image);
                frame.Crop(new MagickGeometry(x * frameWidth, y * frameHeight, (uint)frameWidth, (uint)frameHeight));
                var outputPath = System.IO.Path.Combine(folderPath, $"frame_{count:D4}.png");
                frame.Write(outputPath, MagickFormat.Png);
                count++;
            }
        }

        MessageBox.Show($"Exported {count} frames to {folderPath}");
    }

    private async void SpriteToGifPreviewAnim_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentSpriteSheetPath)) return;

        var columns = int.Parse(SpriteColumns.Text);
        var rows = int.Parse(SpriteRows.Text);
        var frameWidth = int.Parse(SpriteFrameWidth.Text);
        var frameHeight = int.Parse(SpriteFrameHeight.Text);

        using var image = new MagickImage(_currentSpriteSheetPath);
        
        if (frameWidth == 0) frameWidth = (int)(image.Width / columns);
        if (frameHeight == 0) frameHeight = (int)(image.Height / rows);

        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "giflet_preview.gif");
        var frameDelay = 100;

        var gif = new MagickImageCollection();
        
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var frame = new MagickImage(image);
                frame.Crop(new MagickGeometry(x * frameWidth, y * frameHeight, (uint)frameWidth, (uint)frameHeight));
                frame.AnimationDelay = (ushort)(frameDelay / 10);
                gif.Add(frame);
            }
        }

        await gif.WriteAsync(tempPath, MagickFormat.Gif);

        await Dispatcher.InvokeAsync(() =>
        {
            SpriteToGifPreview.Source = new BitmapImage(new Uri(tempPath));
        });
    }

    private string? _currentGifForSprite;
    private List<BitmapImage> _gifFrames = new();

    private void GifToSpriteOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialog("GIF Image|*.gif", path =>
        {
            _currentGifForSprite = path;
            LoadGifFrames(path);
            if (_gifFrames.Count > 0)
            {
                GifToSpritePreview.Source = _gifFrames[0];
            }
        });
    }

    private void LoadGifFrames(string path)
    {
        _gifFrames.Clear();
        try
        {
            using var gif = new MagickImageCollection(path);
            var width = gif[0].Width;
            var height = gif[0].Height;
            
            foreach (var frame in gif)
            {
                using var ms = new MemoryStream();
                frame.Write(ms, MagickFormat.Png);
                ms.Position = 0;
                
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                _gifFrames.Add(image);
            }
        }
        catch { }
    }

    private void GifToSpriteClear_Click(object sender, RoutedEventArgs e)
    {
        _currentGifForSprite = null;
        _gifFrames.Clear();
        GifToSpritePreview.Source = null;
    }

    private void GifToSpriteExport_Click(object sender, RoutedEventArgs e)
    {
        if (_gifFrames.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            DefaultExt = "png"
        };
        if (dialog.ShowDialog() != true) return;

        var savePath = dialog.FileName;
        var columns = int.Parse(GifSpriteColumns.Text);

            var frameWidth = _gifFrames[0].PixelWidth;
            var frameHeight = _gifFrames[0].PixelHeight;
            var rows = (int)Math.Ceiling((double)_gifFrames.Count / columns);

            var spriteWidth = columns * frameWidth;
            var spriteHeight = rows * frameHeight;

            using var sprite = new MagickImage(MagickColors.Transparent, (uint)spriteWidth, (uint)spriteHeight);

            for (int i = 0; i < _gifFrames.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = col * frameWidth;
                var y = row * frameHeight;

                var framePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"frame_{i}.png");
                using (var fileStream = new FileStream(framePath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(_gifFrames[i]));
                    encoder.Save(fileStream);
                }

                var frameImage = new MagickImage(framePath);
                sprite.Composite(frameImage, x, y, CompositeOperator.Over);
                File.Delete(framePath);
            }

            sprite.Write(savePath, MagickFormat.Png);
            MessageBox.Show("Sprite sheet created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private readonly ObservableCollection<string> _pngToSpriteImages = new();

    private void PngToSpriteAddImages_Click(object sender, RoutedEventArgs e)
    {
        ShowOpenDialogMultiple("PNG Image|*.png", paths =>
        {
            foreach (var path in paths)
            {
                _pngToSpriteImages.Add(path);
            }
        });
    }

    private void PngToSpriteClear_Click(object sender, RoutedEventArgs e)
    {
        _pngToSpriteImages.Clear();
    }

    private async void PngToSpriteExport_Click(object sender, RoutedEventArgs e)
    {
        if (_pngToSpriteImages.Count == 0)
        {
            MessageBox.Show("Please add at least one image.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            DefaultExt = "png"
        };
        if (dialog.ShowDialog() != true) return;

        var savePath = dialog.FileName;
        var columns = int.Parse(PngToSpriteColumns.Text);

            var firstImage = new MagickImage(_pngToSpriteImages[0]);
            var frameWidth = firstImage.Width;
            var frameHeight = firstImage.Height;
            firstImage.Dispose();

            var rows = (int)Math.Ceiling((double)_pngToSpriteImages.Count / columns);

            var spriteWidth = columns * frameWidth;
            var spriteHeight = rows * frameHeight;

            using var sprite = new MagickImage(MagickColors.Transparent, (uint)spriteWidth, (uint)spriteHeight);

            for (int i = 0; i < _pngToSpriteImages.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = col * frameWidth;
                var y = row * frameHeight;

                using var frameImage = new MagickImage(_pngToSpriteImages[i]);
                sprite.Composite(frameImage, (int)x, (int)y, CompositeOperator.Over);
            }

            sprite.Write(savePath, MagickFormat.Png);
            MessageBox.Show("Sprite sheet created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}