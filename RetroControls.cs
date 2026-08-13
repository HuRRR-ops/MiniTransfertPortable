using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MiniTransfertPortable;

internal sealed class RetroButton : Button
{
    private bool _pressed;

    public RetroButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = RetroTheme.Face;
        ForeColor = RetroTheme.Text;
        Font = RetroTheme.UiFont;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseDown(MouseEventArgs mouseEvent)
    {
        if (mouseEvent.Button == MouseButtons.Left && Enabled)
        {
            _pressed = true;
            Capture = true;
            Invalidate();
        }
        base.OnMouseDown(mouseEvent);
    }

    protected override void OnMouseUp(MouseEventArgs mouseEvent)
    {
        _pressed = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(mouseEvent);
    }

    protected override void OnMouseLeave(EventArgs eventArgs)
    {
        if (!Capture)
        {
            _pressed = false;
            Invalidate();
        }
        base.OnMouseLeave(eventArgs);
    }

    protected override void OnKeyDown(KeyEventArgs keyEvent)
    {
        if (Enabled && keyEvent.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnKeyDown(keyEvent);
    }

    protected override void OnKeyUp(KeyEventArgs keyEvent)
    {
        _pressed = false;
        Invalidate();
        base.OnKeyUp(keyEvent);
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        var graphics = paintEvent.Graphics;
        graphics.Clear(RetroTheme.Face);
        var pressed = _pressed || (Capture && ClientRectangle.Contains(PointToClient(Cursor.Position)));
        RetroTheme.DrawRaised(graphics, ClientRectangle, pressed);

        var textBounds = Rectangle.Inflate(ClientRectangle, -4, -3);
        if (pressed)
        {
            textBounds.Offset(1, 1);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        if (UseMnemonic)
        {
            flags |= TextFormatFlags.NoPadding;
        }
        else
        {
            flags |= TextFormatFlags.NoPrefix;
        }

        if (Enabled)
        {
            TextRenderer.DrawText(graphics, Text, Font, textBounds, ForeColor, flags);
        }
        else
        {
            var disabled = textBounds;
            disabled.Offset(1, 1);
            TextRenderer.DrawText(graphics, Text, Font, disabled, RetroTheme.Light, flags);
            TextRenderer.DrawText(graphics, Text, Font, textBounds, RetroTheme.DisabledText, flags);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            RetroTheme.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -4, -4));
        }
    }
}

internal sealed class RetroGroupBox : GroupBox
{
    public RetroGroupBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = RetroTheme.Face;
        ForeColor = RetroTheme.Text;
        Font = RetroTheme.UiFont;
        Padding = new Padding(6, 16, 6, 5);
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        var graphics = paintEvent.Graphics;
        graphics.Clear(BackColor);
        var textSize = TextRenderer.MeasureText(graphics, Text, Font, Size.Empty, TextFormatFlags.NoPadding);
        var lineY = Math.Max(7, textSize.Height / 2);
        using var shadow = new Pen(RetroTheme.Shadow);
        using var light = new Pen(RetroTheme.Light);
        var textLeft = 8;
        var gapRight = textLeft + textSize.Width + 3;

        graphics.DrawLine(shadow, 0, lineY, textLeft - 2, lineY);
        graphics.DrawLine(shadow, gapRight, lineY, Width - 2, lineY);
        graphics.DrawLine(shadow, 0, lineY, 0, Height - 2);
        graphics.DrawLine(shadow, Width - 2, lineY, Width - 2, Height - 2);
        graphics.DrawLine(shadow, 0, Height - 2, Width - 2, Height - 2);

        graphics.DrawLine(light, 1, lineY + 1, textLeft - 2, lineY + 1);
        graphics.DrawLine(light, gapRight, lineY + 1, Width - 3, lineY + 1);
        graphics.DrawLine(light, 1, lineY + 1, 1, Height - 3);
        graphics.DrawLine(light, Width - 1, lineY + 1, Width - 1, Height - 1);
        graphics.DrawLine(light, 1, Height - 1, Width - 1, Height - 1);
        TextRenderer.DrawText(graphics, Text, Font, new Point(textLeft, 0), ForeColor, TextFormatFlags.NoPadding);
    }
}

internal sealed class RetroProgressBar : Control
{
    private int _maximum = 100;
    private int _value;

    public RetroProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = RetroTheme.Face;
        Size = new Size(100, 18);
        TabStop = false;
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            Value = Math.Min(_value, _maximum);
            Invalidate();
        }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            var bounded = Math.Clamp(value, 0, _maximum);
            if (_value == bounded)
            {
                return;
            }
            _value = bounded;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        var graphics = paintEvent.Graphics;
        graphics.Clear(RetroTheme.Face);
        RetroTheme.DrawSunken(graphics, ClientRectangle);
        var inside = Rectangle.Inflate(ClientRectangle, -3, -3);
        graphics.FillRectangle(Brushes.White, inside);
        if (_value <= 0 || inside.Width <= 0)
        {
            return;
        }

        var filledWidth = (int)Math.Round(inside.Width * ((double)_value / _maximum));
        var segmentWidth = Math.Max(5, ScaleLogical(7));
        var gap = Math.Max(1, ScaleLogical(2));
        using var fill = new SolidBrush(RetroTheme.Progress);
        for (var x = inside.Left; x < inside.Left + filledWidth; x += segmentWidth + gap)
        {
            var width = Math.Min(segmentWidth, inside.Left + filledWidth - x);
            if (width > 0)
            {
                graphics.FillRectangle(fill, x, inside.Top, width, inside.Height);
            }
        }
    }

    private int ScaleLogical(int value) => Math.Max(1, value * DeviceDpi / 96);
}

internal sealed class RetroStatusBar : Control
{
    private string _mainText = "Prêt";
    private string _itemText = "0 fichier";
    private string _serverText = "Serveur arrêté";

    public RetroStatusBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = RetroTheme.Face;
        ForeColor = RetroTheme.Text;
        Font = RetroTheme.UiFont;
        Height = 22;
        Dock = DockStyle.Bottom;
        TabStop = false;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textHeight = TextRenderer.MeasureText("Serveur arrêté", Font, Size.Empty, TextFormatFlags.NoPadding).Height;
        return new Size(proposedSize.Width, Math.Max(ScaleLogical(26), textHeight + ScaleLogical(12)));
    }

    [AllowNull]
    public override string Text
    {
        get => _mainText;
        set
        {
            _mainText = value ?? string.Empty;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ItemText
    {
        get => _itemText;
        set
        {
            _itemText = value ?? string.Empty;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ServerText
    {
        get => _serverText;
        set
        {
            _serverText = value ?? string.Empty;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        var graphics = paintEvent.Graphics;
        graphics.Clear(BackColor);
        var y = ScaleLogical(2);
        var height = Math.Max(ScaleLogical(18), Height - ScaleLogical(4));
        var serverWidth = Math.Min(170, Math.Max(120, Width / 5));
        var itemWidth = Math.Min(130, Math.Max(95, Width / 7));
        var mainWidth = Math.Max(80, Width - serverWidth - itemWidth - 13);
        var first = new Rectangle(2, y, mainWidth, height);
        var second = new Rectangle(first.Right + 3, y, itemWidth, height);
        var third = new Rectangle(second.Right + 3, y, Math.Max(1, Width - second.Right - 5), height);
        DrawPanel(graphics, first, _mainText);
        DrawPanel(graphics, second, _itemText);
        DrawPanel(graphics, third, _serverText);
    }

    private void DrawPanel(Graphics graphics, Rectangle bounds, string text)
    {
        RetroTheme.DrawSunken(graphics, bounds);
        var textBounds = Rectangle.Inflate(bounds, -4, -2);
        TextRenderer.DrawText(
            graphics,
            text,
            Font,
            textBounds,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    private int ScaleLogical(int value) => Math.Max(1, value * DeviceDpi / 96);
}

internal sealed class RetroTitleBar : Control
{
    private readonly PictureBox _icon = new();
    private readonly Label _caption = new();
    private readonly RetroCaptionButton _minimize = new(RetroCaptionGlyph.Minimize);
    private readonly RetroCaptionButton _maximize = new(RetroCaptionGlyph.Maximize);
    private readonly RetroCaptionButton _close = new(RetroCaptionGlyph.Close);
    private bool _showMaximize = true;
    private bool _showMinimize = true;

    public RetroTitleBar()
    {
        BackColor = RetroTheme.TitleActive;
        Height = 22;
        Dock = DockStyle.Top;
        Padding = new Padding(3, 3, 3, 3);
        _icon.SizeMode = PictureBoxSizeMode.CenterImage;
        _icon.Size = new Size(18, 16);
        _icon.Location = new Point(2, 3);
        _icon.Image = RetroIcons.CreateApplicationBitmap(16);
        _caption.Text = "MiniTransfert Portable";
        _caption.ForeColor = Color.White;
        _caption.BackColor = Color.Transparent;
        _caption.Font = RetroTheme.BoldFont;
        _caption.AutoEllipsis = true;
        _caption.TextAlign = ContentAlignment.MiddleLeft;
        _caption.Location = new Point(22, 2);
        _caption.Height = 18;

        Controls.AddRange([_icon, _caption, _minimize, _maximize, _close]);
        _minimize.Click += (_, _) => FindForm()!.WindowState = FormWindowState.Minimized;
        _maximize.Click += (_, _) => ToggleMaximize();
        _close.Click += (_, _) => FindForm()!.Close();
        MouseDown += BeginWindowDrag;
        _caption.MouseDown += BeginWindowDrag;
        _icon.MouseDown += BeginWindowDrag;
        DoubleClick += (_, _) => ToggleMaximize();
        _caption.DoubleClick += (_, _) => ToggleMaximize();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Caption
    {
        get => _caption.Text;
        set => _caption.Text = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MaximizeVisible
    {
        get => _showMaximize;
        set
        {
            _showMaximize = value;
            _maximize.Visible = value;
            PerformLayout();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MinimizeVisible
    {
        get => _showMinimize;
        set
        {
            _showMinimize = value;
            _minimize.Visible = value;
            PerformLayout();
            Invalidate();
        }
    }

    public void SyncWindowState(FormWindowState state)
    {
        _maximize.Glyph = state == FormWindowState.Maximized
            ? RetroCaptionGlyph.Restore
            : RetroCaptionGlyph.Maximize;
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        var buttonWidth = ScaleLogical(19);
        var buttonHeight = ScaleLogical(18);
        var top = ScaleLogical(2);
        var edge = ScaleLogical(3);
        var gap = ScaleLogical(2);
        _icon.SetBounds(ScaleLogical(2), ScaleLogical(3), ScaleLogical(18), ScaleLogical(16));
        _caption.SetBounds(ScaleLogical(22), ScaleLogical(2), _caption.Width, ScaleLogical(18));
        _close.SetBounds(Width - buttonWidth - edge, top, buttonWidth, buttonHeight);
        _maximize.SetBounds(_close.Left - buttonWidth - gap, top, buttonWidth, buttonHeight);
        _minimize.SetBounds((_showMaximize ? _maximize.Left : _close.Left) - buttonWidth - gap, top, buttonWidth, buttonHeight);
        var firstButtonLeft = _showMinimize
            ? _minimize.Left
            : _showMaximize
                ? _maximize.Left
                : _close.Left;
        _caption.Width = Math.Max(ScaleLogical(20), firstButtonLeft - _caption.Left - edge);
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        paintEvent.Graphics.Clear(FindForm()?.ContainsFocus == false ? RetroTheme.TitleInactive : RetroTheme.TitleActive);
    }

    private void BeginWindowDrag(object? sender, MouseEventArgs mouseEvent)
    {
        if (mouseEvent.Button != MouseButtons.Left)
        {
            return;
        }
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(FindForm()!.Handle, NativeMethods.WmNcLeftButtonDown, (IntPtr)NativeMethods.HtCaption, IntPtr.Zero);
    }

    private void ToggleMaximize()
    {
        var form = FindForm();
        if (form is null)
        {
            return;
        }
        form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        _maximize.Glyph = form.WindowState == FormWindowState.Maximized ? RetroCaptionGlyph.Restore : RetroCaptionGlyph.Maximize;
    }

    private int ScaleLogical(int value) => Math.Max(1, value * DeviceDpi / 96);
}

internal enum RetroCaptionGlyph
{
    Minimize,
    Maximize,
    Restore,
    Close
}

internal sealed class RetroCaptionButton : Control
{
    private bool _pressed;
    private RetroCaptionGlyph _glyph;

    public RetroCaptionButton(RetroCaptionGlyph glyph)
    {
        _glyph = glyph;
        BackColor = RetroTheme.Face;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        TabStop = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RetroCaptionGlyph Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs mouseEvent)
    {
        if (mouseEvent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Capture = true;
            Invalidate();
        }
        base.OnMouseDown(mouseEvent);
    }

    protected override void OnMouseUp(MouseEventArgs mouseEvent)
    {
        _pressed = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(mouseEvent);
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        var graphics = paintEvent.Graphics;
        graphics.Clear(RetroTheme.Face);
        RetroTheme.DrawRaised(graphics, ClientRectangle, _pressed);
        var offset = _pressed ? 1 : 0;
        using var pen = new Pen(Color.Black, 1);
        using var brush = new SolidBrush(Color.Black);
        var centerX = Width / 2 + offset;
        var centerY = Height / 2 + offset;
        switch (_glyph)
        {
            case RetroCaptionGlyph.Minimize:
                graphics.FillRectangle(brush, centerX - 4, centerY + 3, 7, 2);
                break;
            case RetroCaptionGlyph.Maximize:
                graphics.DrawRectangle(pen, centerX - 4, centerY - 4, 8, 8);
                graphics.DrawLine(pen, centerX - 3, centerY - 3, centerX + 3, centerY - 3);
                break;
            case RetroCaptionGlyph.Restore:
                graphics.DrawRectangle(pen, centerX - 2, centerY - 4, 7, 7);
                graphics.DrawRectangle(pen, centerX - 5, centerY - 1, 7, 7);
                break;
            case RetroCaptionGlyph.Close:
                for (var index = 0; index < 2; index++)
                {
                    graphics.DrawLine(pen, centerX - 4, centerY - 4 + index, centerX + 4, centerY + 4 + index);
                    graphics.DrawLine(pen, centerX + 4, centerY - 4 + index, centerX - 4, centerY + 4 + index);
                }
                break;
        }
    }
}

internal class RetroChromeForm : Form
{
    private const int LogicalResizeBorder = 4;

    protected RetroChromeForm(string caption, bool allowMaximize = true, bool allowResize = true)
    {
        Text = caption;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = RetroTheme.Face;
        ForeColor = RetroTheme.Text;
        Font = RetroTheme.UiFont;
        Padding = new Padding(3);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        MaximizeBox = allowMaximize;
        MinimizeBox = true;
        AllowResize = allowResize;
        SetStyle(ControlStyles.ResizeRedraw, true);
        Icon = RetroIcons.CreateApplicationIcon();
        TitleBar = new RetroTitleBar { Caption = caption };
        TitleBar.MaximizeVisible = allowMaximize;
        TitleBar.MinimizeVisible = allowResize || allowMaximize;
        ContentPanel = new Panel
        {
            Dock = DockStyle.None,
            BackColor = RetroTheme.Face,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        TitleBar.Dock = DockStyle.None;
        Controls.Add(ContentPanel);
        Controls.Add(TitleBar);
        TitleBar.BringToFront();
        ArrangeChrome();
    }

    protected RetroTitleBar TitleBar { get; }
    protected Panel ContentPanel { get; }
    protected bool AllowResize { get; }

    protected override void OnActivated(EventArgs eventArgs)
    {
        base.OnActivated(eventArgs);
        TitleBar.Invalidate();
    }

    protected override void OnDeactivate(EventArgs eventArgs)
    {
        base.OnDeactivate(eventArgs);
        TitleBar.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        base.OnPaint(paintEvent);
        var bounds = ClientRectangle;
        RetroTheme.DrawRaised(paintEvent.Graphics, bounds);
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        ArrangeChrome();
        TitleBar?.SyncWindowState(WindowState);
    }

    protected override void OnShown(EventArgs eventArgs)
    {
        UpdateMaximizedBounds();
        base.OnShown(eventArgs);
    }

    protected override void OnLocationChanged(EventArgs eventArgs)
    {
        base.OnLocationChanged(eventArgs);
        if (WindowState == FormWindowState.Normal && IsHandleCreated)
        {
            UpdateMaximizedBounds();
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (AllowResize && message.Msg == NativeMethods.WmNcHitTest && WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref message);
            if ((int)message.Result == NativeMethods.HtClient)
            {
                var point = PointToClient(NativeMethods.PointFromLParam(message.LParam));
                var resizeBorder = ScaleLogical(LogicalResizeBorder);
                var left = point.X <= resizeBorder;
                var right = point.X >= ClientSize.Width - resizeBorder;
                var top = point.Y <= resizeBorder;
                var bottom = point.Y >= ClientSize.Height - resizeBorder;
                message.Result = (IntPtr)(
                    top && left ? NativeMethods.HtTopLeft :
                    top && right ? NativeMethods.HtTopRight :
                    bottom && left ? NativeMethods.HtBottomLeft :
                    bottom && right ? NativeMethods.HtBottomRight :
                    left ? NativeMethods.HtLeft :
                    right ? NativeMethods.HtRight :
                    top ? NativeMethods.HtTop :
                    bottom ? NativeMethods.HtBottom :
                    NativeMethods.HtClient);
            }
            return;
        }
        base.WndProc(ref message);
    }

    private void ArrangeChrome()
    {
        if (TitleBar is null || ContentPanel is null)
        {
            return;
        }
        var border = ScaleLogical(3);
        var titleHeight = ScaleLogical(22);
        var innerWidth = Math.Max(0, ClientSize.Width - (border * 2));
        TitleBar.SetBounds(border, border, innerWidth, titleHeight);
        ContentPanel.SetBounds(
            border,
            border + titleHeight,
            innerWidth,
            Math.Max(0, ClientSize.Height - titleHeight - (border * 2)));
    }

    private void UpdateMaximizedBounds()
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
    }

    private int ScaleLogical(int value) => Math.Max(1, value * DeviceDpi / 96);
}

internal static class NativeMethods
{
    public const int WmNcHitTest = 0x0084;
    public const int WmNcLeftButtonDown = 0x00A1;
    public const int HtClient = 1;
    public const int HtCaption = 2;
    public const int HtLeft = 10;
    public const int HtRight = 11;
    public const int HtTop = 12;
    public const int HtTopLeft = 13;
    public const int HtTopRight = 14;
    public const int HtBottom = 15;
    public const int HtBottomLeft = 16;
    public const int HtBottomRight = 17;

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    public static Point PointFromLParam(IntPtr value)
    {
        var packed = value.ToInt64();
        return new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff)));
    }
}

internal sealed class RetroMenuRenderer : ToolStripSystemRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs eventArgs)
    {
        eventArgs.Graphics.Clear(RetroTheme.Face);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs eventArgs)
    {
        var item = eventArgs.Item;
        if (item.Selected || item.Pressed)
        {
            using var selection = new SolidBrush(RetroTheme.Selection);
            eventArgs.Graphics.FillRectangle(selection, new Rectangle(Point.Empty, item.Size));
            item.ForeColor = Color.White;
        }
        else
        {
            using var face = new SolidBrush(RetroTheme.Face);
            eventArgs.Graphics.FillRectangle(face, new Rectangle(Point.Empty, item.Size));
            item.ForeColor = item.Enabled ? RetroTheme.Text : RetroTheme.DisabledText;
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs eventArgs)
    {
        var y = eventArgs.Item.Height / 2;
        RetroTheme.DrawEtchedHorizontal(eventArgs.Graphics, 2, eventArgs.Item.Width - 3, y);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs eventArgs)
    {
        using var shadow = new Pen(RetroTheme.Shadow);
        using var light = new Pen(RetroTheme.Light);
        eventArgs.Graphics.DrawLine(shadow, 0, eventArgs.ToolStrip.Height - 2, eventArgs.ToolStrip.Width, eventArgs.ToolStrip.Height - 2);
        eventArgs.Graphics.DrawLine(light, 0, eventArgs.ToolStrip.Height - 1, eventArgs.ToolStrip.Width, eventArgs.ToolStrip.Height - 1);
    }
}
