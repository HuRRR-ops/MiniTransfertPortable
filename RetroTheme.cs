using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MiniTransfertPortable;

internal static class RetroTheme
{
    public static readonly Color Face = Color.FromArgb(192, 192, 192);
    public static readonly Color Light = Color.White;
    public static readonly Color Highlight = Color.FromArgb(223, 223, 223);
    public static readonly Color Shadow = Color.FromArgb(128, 128, 128);
    public static readonly Color DarkShadow = Color.FromArgb(64, 64, 64);
    public static readonly Color Window = Color.White;
    public static readonly Color Text = Color.Black;
    public static readonly Color DisabledText = Color.FromArgb(128, 128, 128);
    public static readonly Color TitleActive = Color.FromArgb(0, 0, 128);
    public static readonly Color TitleInactive = Color.FromArgb(128, 128, 128);
    public static readonly Color Selection = Color.FromArgb(0, 0, 128);
    public static readonly Color Progress = Color.FromArgb(0, 128, 0);

    public static Font UiFont => new("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
    public static Font BoldFont => new("Microsoft Sans Serif", 8.25F, FontStyle.Bold, GraphicsUnit.Point);
    public static Font LogFont => new("Courier New", 8.25F, FontStyle.Regular, GraphicsUnit.Point);

    public static void DrawRaised(Graphics graphics, Rectangle bounds, bool pressed = false)
    {
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        var topLeftOuter = pressed ? DarkShadow : Light;
        var topLeftInner = pressed ? Shadow : Highlight;
        var bottomRightOuter = pressed ? Light : DarkShadow;
        var bottomRightInner = pressed ? Highlight : Shadow;
        DrawBoxLines(graphics, bounds, topLeftOuter, topLeftInner, bottomRightOuter, bottomRightInner);
    }

    public static void DrawSunken(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }
        DrawBoxLines(graphics, bounds, Shadow, DarkShadow, Light, Highlight);
    }

    public static void DrawEtchedHorizontal(Graphics graphics, int x1, int x2, int y)
    {
        using var shadow = new Pen(Shadow);
        using var light = new Pen(Light);
        graphics.DrawLine(shadow, x1, y, x2, y);
        graphics.DrawLine(light, x1, y + 1, x2, y + 1);
    }

    public static void DrawFocusRectangle(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width > 2 && bounds.Height > 2)
        {
            ControlPaint.DrawFocusRectangle(graphics, bounds, Text, Face);
        }
    }

    private static void DrawBoxLines(
        Graphics graphics,
        Rectangle bounds,
        Color topLeftOuter,
        Color topLeftInner,
        Color bottomRightOuter,
        Color bottomRightInner)
    {
        using var outerTopLeft = new Pen(topLeftOuter);
        using var innerTopLeft = new Pen(topLeftInner);
        using var outerBottomRight = new Pen(bottomRightOuter);
        using var innerBottomRight = new Pen(bottomRightInner);
        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right - 1;
        var bottom = bounds.Bottom - 1;

        graphics.DrawLine(outerTopLeft, left, bottom - 1, left, top);
        graphics.DrawLine(outerTopLeft, left, top, right - 1, top);
        graphics.DrawLine(outerBottomRight, left, bottom, right, bottom);
        graphics.DrawLine(outerBottomRight, right, bottom, right, top);

        graphics.DrawLine(innerTopLeft, left + 1, bottom - 2, left + 1, top + 1);
        graphics.DrawLine(innerTopLeft, left + 1, top + 1, right - 2, top + 1);
        graphics.DrawLine(innerBottomRight, left + 1, bottom - 1, right - 1, bottom - 1);
        graphics.DrawLine(innerBottomRight, right - 1, bottom - 1, right - 1, top + 1);
    }
}

internal static class RetroIcons
{
    public const string Application = "application";
    public const string File = "file";
    public const string Folder = "folder";
    public const string Transfer = "transfer";
    public const string Complete = "complete";
    public const string Error = "error";

    public static Bitmap CreateApplicationBitmap(int size)
    {
        var bitmap = NewPixelBitmap(16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        using var navy = new SolidBrush(Color.FromArgb(0, 0, 128));
        using var blue = new SolidBrush(Color.FromArgb(0, 128, 192));
        using var gray = new SolidBrush(RetroTheme.Face);
        using var dark = new SolidBrush(RetroTheme.DarkShadow);
        using var white = new SolidBrush(Color.White);
        using var yellow = new SolidBrush(Color.FromArgb(255, 255, 0));

        graphics.FillRectangle(dark, 1, 2, 6, 5);
        graphics.FillRectangle(gray, 2, 3, 4, 3);
        graphics.FillRectangle(blue, 3, 3, 2, 2);
        graphics.FillRectangle(dark, 2, 8, 5, 4);
        graphics.FillRectangle(gray, 3, 9, 3, 2);
        graphics.FillRectangle(dark, 9, 2, 6, 5);
        graphics.FillRectangle(gray, 10, 3, 4, 3);
        graphics.FillRectangle(navy, 11, 3, 2, 2);
        graphics.FillRectangle(dark, 9, 8, 5, 4);
        graphics.FillRectangle(gray, 10, 9, 3, 2);
        graphics.FillRectangle(yellow, 6, 6, 4, 2);
        graphics.FillRectangle(yellow, 8, 5, 2, 4);
        graphics.FillRectangle(white, 7, 6, 1, 1);
        graphics.FillRectangle(dark, 5, 12, 6, 2);

        bitmap.MakeTransparent(Color.Fuchsia);
        return size == 16 ? bitmap : ScalePixelArt(bitmap, size);
    }

    public static Icon CreateApplicationIcon()
    {
        using var bitmap = CreateApplicationBitmap(32);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public static ImageList CreateSmallImageList()
    {
        var list = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16),
            TransparentColor = Color.Fuchsia
        };
        list.Images.Add(Application, CreateApplicationBitmap(16));
        list.Images.Add(File, CreateFileBitmap());
        list.Images.Add(Folder, CreateFolderBitmap());
        list.Images.Add(Transfer, CreateTransferBitmap());
        list.Images.Add(Complete, CreateCompleteBitmap());
        list.Images.Add(Error, CreateErrorBitmap());
        return list;
    }

    public static Bitmap CreateDialogBitmap(MessageBoxIcon icon)
    {
        var bitmap = NewPixelBitmap(32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        graphics.SmoothingMode = SmoothingMode.None;
        using var dark = new SolidBrush(Color.Black);
        using var white = new SolidBrush(Color.White);

        if (icon == MessageBoxIcon.Error)
        {
            using var red = new SolidBrush(Color.FromArgb(192, 0, 0));
            graphics.FillEllipse(dark, 1, 1, 30, 30);
            graphics.FillEllipse(red, 3, 3, 26, 26);
            graphics.FillRectangle(white, 9, 8, 4, 4);
            graphics.FillRectangle(white, 19, 8, 4, 4);
            graphics.FillRectangle(white, 12, 11, 4, 4);
            graphics.FillRectangle(white, 16, 15, 4, 4);
            graphics.FillRectangle(white, 9, 19, 4, 4);
            graphics.FillRectangle(white, 19, 19, 4, 4);
            graphics.FillRectangle(white, 12, 18, 4, 4);
        }
        else if (icon == MessageBoxIcon.Warning)
        {
            using var yellow = new SolidBrush(Color.FromArgb(255, 255, 0));
            graphics.FillPolygon(dark, [new Point(16, 1), new Point(31, 29), new Point(1, 29)]);
            graphics.FillPolygon(yellow, [new Point(16, 4), new Point(28, 27), new Point(4, 27)]);
            graphics.FillRectangle(dark, 14, 10, 4, 9);
            graphics.FillRectangle(dark, 14, 22, 4, 4);
        }
        else
        {
            using var blue = new SolidBrush(Color.FromArgb(0, 0, 192));
            graphics.FillEllipse(dark, 1, 1, 30, 30);
            graphics.FillEllipse(blue, 3, 3, 26, 26);
            graphics.FillRectangle(white, 14, 7, 4, 4);
            graphics.FillRectangle(white, 14, 14, 4, 11);
        }

        bitmap.MakeTransparent(Color.Fuchsia);
        return bitmap;
    }

    private static Bitmap CreateFileBitmap()
    {
        var bitmap = NewPixelBitmap(16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        using var white = new SolidBrush(Color.White);
        using var gray = new SolidBrush(RetroTheme.Face);
        using var dark = new SolidBrush(RetroTheme.DarkShadow);
        graphics.FillRectangle(dark, 3, 1, 9, 14);
        graphics.FillRectangle(white, 4, 2, 7, 12);
        graphics.FillRectangle(gray, 9, 2, 2, 3);
        graphics.FillRectangle(dark, 9, 4, 3, 1);
        graphics.FillRectangle(gray, 5, 7, 5, 1);
        graphics.FillRectangle(gray, 5, 9, 5, 1);
        graphics.FillRectangle(gray, 5, 11, 4, 1);
        bitmap.MakeTransparent(Color.Fuchsia);
        return bitmap;
    }

    private static Bitmap CreateFolderBitmap()
    {
        var bitmap = NewPixelBitmap(16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        using var yellow = new SolidBrush(Color.FromArgb(255, 255, 0));
        using var olive = new SolidBrush(Color.FromArgb(128, 128, 0));
        using var white = new SolidBrush(Color.White);
        graphics.FillRectangle(olive, 1, 4, 14, 10);
        graphics.FillRectangle(olive, 2, 2, 6, 3);
        graphics.FillRectangle(yellow, 2, 5, 12, 8);
        graphics.FillRectangle(yellow, 3, 3, 4, 2);
        graphics.FillRectangle(white, 3, 6, 10, 1);
        bitmap.MakeTransparent(Color.Fuchsia);
        return bitmap;
    }

    private static Bitmap CreateTransferBitmap()
    {
        var bitmap = NewPixelBitmap(16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        using var blue = new SolidBrush(Color.FromArgb(0, 0, 192));
        using var cyan = new SolidBrush(Color.FromArgb(0, 192, 255));
        graphics.FillRectangle(blue, 1, 6, 11, 4);
        graphics.FillRectangle(blue, 9, 3, 3, 10);
        graphics.FillRectangle(blue, 12, 5, 2, 6);
        graphics.FillRectangle(cyan, 3, 7, 7, 1);
        bitmap.MakeTransparent(Color.Fuchsia);
        return bitmap;
    }

    private static Bitmap CreateCompleteBitmap()
    {
        var bitmap = NewPixelBitmap(16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        using var green = new SolidBrush(Color.FromArgb(0, 128, 0));
        using var light = new SolidBrush(Color.FromArgb(0, 255, 0));
        graphics.FillRectangle(green, 2, 8, 3, 3);
        graphics.FillRectangle(green, 4, 10, 3, 3);
        graphics.FillRectangle(green, 6, 7, 3, 4);
        graphics.FillRectangle(green, 8, 5, 3, 4);
        graphics.FillRectangle(green, 10, 3, 3, 4);
        graphics.FillRectangle(light, 4, 9, 2, 1);
        graphics.FillRectangle(light, 7, 7, 2, 1);
        bitmap.MakeTransparent(Color.Fuchsia);
        return bitmap;
    }

    private static Bitmap CreateErrorBitmap()
    {
        var bitmap = NewPixelBitmap(16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Fuchsia);
        using var red = new SolidBrush(Color.FromArgb(192, 0, 0));
        using var white = new SolidBrush(Color.White);
        graphics.FillRectangle(red, 3, 2, 10, 12);
        graphics.FillRectangle(red, 2, 3, 12, 10);
        graphics.FillRectangle(white, 5, 5, 2, 2);
        graphics.FillRectangle(white, 9, 5, 2, 2);
        graphics.FillRectangle(white, 7, 7, 2, 2);
        graphics.FillRectangle(white, 5, 9, 2, 2);
        graphics.FillRectangle(white, 9, 9, 2, 2);
        bitmap.MakeTransparent(Color.Fuchsia);
        return bitmap;
    }

    private static Bitmap NewPixelBitmap(int size) => new(size, size, PixelFormat.Format32bppArgb);

    private static Bitmap ScalePixelArt(Bitmap source, int size)
    {
        var target = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(target);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(source, new Rectangle(0, 0, size, size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
        return target;
    }
}
