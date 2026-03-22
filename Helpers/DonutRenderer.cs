#if WINDOWS
using SkiaSharp;

namespace copilot_usage_maui.Helpers;

/// <summary>
/// SkiaSharp 기반 도넛 차트 렌더러.
/// Widget(Deskband/Floating), Popup에서 공통 사용.
/// </summary>
static class DonutRenderer
{
    /// <summary>
    /// 도넛 차트를 SKBitmap으로 렌더링
    /// </summary>
    /// <param name="size">비트맵 크기 (정사각형)</param>
    /// <param name="strokeWidth">도넛 두께</param>
    /// <param name="percent">0-100 사용률</param>
    /// <param name="trackColor">배경 트랙 색상</param>
    /// <param name="fillColor">채움 색상</param>
    /// <param name="centerText">중앙 텍스트 (null이면 표시 안 함)</param>
    /// <param name="textColor">중앙 텍스트 색상</param>
    /// <param name="fontSize">중앙 텍스트 크기</param>
    /// <param name="scale">DPI 스케일 (기본 1.0)</param>
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
            // 12시 방향(-90°)에서 시작, 시계방향
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
            // 수직 중앙 정렬
            var metrics = font.Metrics;
            float textY = cy - (metrics.Ascent + metrics.Descent) / 2f;
            canvas.DrawText(centerText, cx, textY, SKTextAlign.Center, font, textPaint);
        }

        return bitmap;
    }

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
    /// 사용률에 따른 상태 색상 반환 (60/80 임계값)
    /// </summary>
    public static SKColor GetStatusColor(double percent, bool isDark = false)
    {
        if (percent >= 80)
            return isDark ? SKColor.Parse("#F09595") : SKColor.Parse("#E24B4A");
        if (percent >= 60)
            return isDark ? SKColor.Parse("#FAC775") : SKColor.Parse("#EF9F27");
        return isDark ? SKColor.Parse("#5DCAA5") : SKColor.Parse("#1D9E75");
    }

    /// <summary>
    /// 사용률에 따른 Win32 Color 반환 (위젯용)
    /// </summary>
    public static Windows.UI.Color GetStatusWinColor(double percent)
    {
        if (percent >= 80)
            return Microsoft.UI.ColorHelper.FromArgb(255, 226, 75, 74);  // #E24B4A
        if (percent >= 60)
            return Microsoft.UI.ColorHelper.FromArgb(255, 239, 159, 39); // #EF9F27
        return Microsoft.UI.ColorHelper.FromArgb(255, 29, 158, 117);     // #1D9E75
    }

    /// <summary>
    /// 도넛 트랙(배경) 색상
    /// </summary>
    public static SKColor GetTrackColor(bool isDark)
        => isDark ? SKColor.Parse("#444444") : SKColor.Parse("#E8E6E1");
}
#endif
