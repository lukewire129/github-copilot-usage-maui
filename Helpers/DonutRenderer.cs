using SkiaSharp;

namespace copilot_usage_maui.Helpers;

/// <summary>
/// SkiaSharp 기반 도넛 차트 렌더러.
/// Widget, Dashboard 페이지에서 공통 사용.
/// </summary>
static class DonutRenderer
{
    static bool IsDark => MauiControls.Application.Current?.RequestedTheme == AppTheme.Dark;
    /// <summary>
    /// 도넛 차트를 SKBitmap으로 렌더링
    /// </summary>
    public static SKBitmap Render(
        int size,
        float strokeWidth,
        double percent,
        SKColor trackColor,
        SKColor fillColor,
        string? centerText = null,
        SKColor? textColor = null,
        float fontSize = 0,
        float scale = 1f)
    {
        int scaledSize = (int)(size * scale);
        float scaledStroke = strokeWidth * scale;
        float scaledFontSize = fontSize * scale;

        var bitmap = new SKBitmap(scaledSize, scaledSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        float cx = scaledSize / 2f;
        float cy = scaledSize / 2f;
        float radius = (scaledSize - scaledStroke) / 2f;

        var rect = new SKRect(
            cx - radius, cy - radius,
            cx + radius, cy + radius);

        // Track (배경 원)
        using var trackPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = scaledStroke,
            Color = trackColor,
            IsAntialias = true,
        };
        canvas.DrawCircle(cx, cy, radius, trackPaint);

        // Fill (사용량 arc)
        if (percent > 0)
        {
            float sweepAngle = (float)(Math.Min(percent, 100) / 100.0 * 360.0);
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = scaledStroke,
                StrokeCap = SKStrokeCap.Round,
                Color = fillColor,
                IsAntialias = true,
            };
            canvas.DrawArc(rect, -90f, sweepAngle, false, fillPaint);
        }

        // Center text
        if (centerText is not null && scaledFontSize > 0)
        {
            using var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
            using var font = new SKFont(typeface, scaledFontSize);
            using var textPaint = new SKPaint
            {
                Color = textColor ?? fillColor,
                IsAntialias = true,
            };
            var metrics = font.Metrics;
            float textY = cy - (metrics.Ascent + metrics.Descent) / 2f;
            canvas.DrawText(centerText, cx, textY, SKTextAlign.Center, font, textPaint);
        }

        return bitmap;
    }

    /// <summary>
    /// SKCanvasView의 PaintSurface에서 직접 도넛을 그리는 메서드
    /// </summary>
    public static void DrawOnCanvas(
        SKCanvas canvas,
        int width,
        int height,
        float strokeWidth,
        double percent,
        SKColor trackColor,
        SKColor fillColor)
    {
        canvas.Clear(SKColors.Transparent);

        int size = Math.Min(width, height);
        float cx = width / 2f;
        float cy = height / 2f;
        float radius = (size - strokeWidth) / 2f;

        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        using var trackPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            Color = trackColor,
            IsAntialias = true,
        };
        canvas.DrawCircle(cx, cy, radius, trackPaint);

        if (percent > 0)
        {
            float sweepAngle = (float)(Math.Min(percent, 100) / 100.0 * 360.0);
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                StrokeCap = SKStrokeCap.Round,
                Color = fillColor,
                IsAntialias = true,
            };
            canvas.DrawArc(rect, -90f, sweepAngle, false, fillPaint);
        }
    }

    /// <summary>
    /// 사용률에 따른 상태 색상 반환 (60/80 임계값)
    /// </summary>
    public static SKColor GetStatusColor(double percent)
    {
        if (percent >= 80)
            return IsDark ? SKColor.Parse("#F09595") : SKColor.Parse("#E24B4A");
        if (percent >= 60)
            return IsDark ? SKColor.Parse("#FAC775") : SKColor.Parse("#EF9F27");
        return IsDark ? SKColor.Parse("#5DCAA5") : SKColor.Parse("#1D9E75");
    }

    /// <summary>
    /// 도넛 트랙(배경) 색상
    /// </summary>
    public static SKColor GetTrackColor()
        => IsDark ? SKColor.Parse("#444444") : SKColor.Parse("#E8E6E1");

#if WINDOWS
    /// <summary>
    /// SKBitmap → WinUI3 BitmapImage (PNG 변환)
    /// </summary>
    public static async Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage> ToWinUIImageAsync(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var ms = new MemoryStream();
        data.SaveTo(ms);
        ms.Position = 0;

        var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        using var ras = ms.AsRandomAccessStream();
        await bitmapImage.SetSourceAsync(ras);
        return bitmapImage;
    }

    /// <summary>
    /// 사용률에 따른 Win32 Color 반환 (위젯용)
    /// </summary>
    public static Windows.UI.Color GetStatusWinColor(double percent)
    {
        if (percent >= 80)
            return Microsoft.UI.ColorHelper.FromArgb(255, 226, 75, 74);
        if (percent >= 60)
            return Microsoft.UI.ColorHelper.FromArgb(255, 239, 159, 39);
        return Microsoft.UI.ColorHelper.FromArgb(255, 29, 158, 117);
    }
#endif
}
