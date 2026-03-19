using Microsoft.Maui.Storage;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Svg.Skia;

namespace copilot_usage_maui.Features.GithubCopilot.Components;

public class SvgIconState
{
    // 필드가 아닌 State에 담아야 핫 리로드 시 복구 확률이 높습니다.
    public SKSvg? Svg { get; set; }
    public string? LoadedFile { get; set; }
}
public partial class SvgIcon : Component<SvgIconState>
{
    [Prop] string _fileName = "ddd.svg";
    [Prop] Color _tintColor = Colors.Black;
    [Prop] float _size = 24f;
    private SkiaSharp.Views.Maui.Controls.SKCanvasView? _canvasView;

    public override VisualNode Render()
    {
        // 핫 리로드 후 State가 비었거나 파일이 바뀌었을 때만 로드
        if (State.Svg == null || State.LoadedFile != _fileName)
        {
            Task.Run(LoadAndInvalidate);
        }
        return new SkiaCanvas(componentRefAction: (refView) => _canvasView = refView)
                    .HeightRequest(_size)
                    .WidthRequest(_size)
                    .OnPaintSurface(OnPaint); // 네이티브 뷰 참조
    }
    private async Task LoadAndInvalidate()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(_fileName);
            var svg = new SKSvg();
            svg.Load(stream);

            SetState(s =>
            {
                s.Svg = svg;
                s.LoadedFile = _fileName;
            });

            _canvasView?.InvalidateSurface();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SVG Load Error: {ex.Message}");
        }
    }

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (State.Svg?.Picture == null) return;

        var bounds = State.Svg.Picture.CullRect;
        var scale = Math.Min(_size / bounds.Width, _size / bounds.Height);

        using var paint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateBlendMode(
                _tintColor.ToSKColor(),
                SKBlendMode.SrcIn)
        };

        canvas.Scale(scale);
        canvas.DrawPicture(State.Svg.Picture, paint);
    }
}