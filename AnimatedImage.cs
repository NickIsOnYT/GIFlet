using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GIFlet;

public class AnimatedImage : Image
{
    private DispatcherTimer? _timer;
    private BitmapDecoder? _decoder;
    private int _currentFrame;
    private WriteableBitmap? _wb;

    public static readonly DependencyProperty SourcePathProperty =
        DependencyProperty.Register(nameof(SourcePath), typeof(string), typeof(AnimatedImage),
            new PropertyMetadata(null, OnSourcePathChanged));

    public string SourcePath
    {
        get => (string)GetValue(SourcePathProperty);
        set => SetValue(SourcePathProperty, value);
    }

    private static void OnSourcePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedImage img && e.NewValue is string path)
        {
            img.LoadGif(path);
        }
    }

    private void LoadGif(string path)
    {
        try
        {
            var uri = new Uri(path);
            _decoder = BitmapDecoder.Create(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            
            if (_decoder.Frames.Count > 1)
            {
                var frame = _decoder.Frames[0];
                _wb = new WriteableBitmap(frame);
                Source = _wb;
                _currentFrame = 0;

                var duration = 100;
                var metadata = frame.Metadata as BitmapMetadata;
                if (metadata != null)
                {
                    try
                    {
                        var delay = metadata.GetQuery("/grctlext/Delay") as ushort?;
                        if (delay.HasValue && delay.Value > 0)
                            duration = delay.Value * 10;
                    }
                    catch { }
                }

                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(duration)
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            else
            {
                var frame = _decoder.Frames[0];
                Source = new WriteableBitmap(frame);
            }
        }
        catch
        {
            // Not a GIF or error loading
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_decoder == null || _decoder.Frames.Count <= 1) return;

        _currentFrame = (_currentFrame + 1) % _decoder.Frames.Count;
        var frame = _decoder.Frames[_currentFrame];
        
        if (_wb == null || _wb.PixelWidth != frame.PixelWidth || _wb.PixelHeight != frame.PixelHeight)
        {
            _wb = new WriteableBitmap(frame);
            Source = _wb;
        }
        
        var duration = 100;
        var metadata = frame.Metadata as BitmapMetadata;
        if (metadata != null)
        {
            try
            {
                var delay = metadata.GetQuery("/grctlext/Delay") as ushort?;
                if (delay.HasValue && delay.Value > 0)
                    duration = delay.Value * 10;
            }
            catch { }
        }
        
        _timer!.Interval = TimeSpan.FromMilliseconds(duration);
    }

    public void Stop()
    {
        _timer?.Stop();
    }
}
