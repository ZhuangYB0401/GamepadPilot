using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Typing;
using WinForms = System.Windows.Forms;

namespace GamepadPilot.App;

public partial class TypingWheelWindow : Window
{
    private const double Center = 270d;
    private const double OuterRadius = 250d;
    private const double InnerRadius = 78d;
    private readonly Brush[] _buttonBrushes =
    [
        new SolidColorBrush(Color.FromRgb(111, 190, 74)),
        new SolidColorBrush(Color.FromRgb(218, 74, 62)),
        new SolidColorBrush(Color.FromRgb(45, 183, 230)),
        new SolidColorBrush(Color.FromRgb(232, 201, 67))
    ];

    public TypingWheelWindow()
    {
        InitializeComponent();
    }

    public void ShowOverlay(TypingWheelState state)
    {
        UpdateState(state);
        if (!IsVisible) Show();
        PositionOnCursorMonitor();
        SetWindowPos(new WindowInteropHelper(this).Handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    public void UpdateState(TypingWheelState state)
    {
        SectorCanvas.Children.Clear();
        for (var index = 0; index < 8; index++)
        {
            var direction = (TypingWheelDirection)index;
            var selected = state.Direction == direction;
            var angle = index * 45d;
            SectorCanvas.Children.Add(CreateSectorGeometry(angle, selected));
            SectorCanvas.Children.Add(CreateGlyphBlock(index, angle, selected, state.LayerIndex, state.ShiftActive));
        }
    }

    private void PositionOnCursorMonitor()
    {
        var screen = WinForms.Screen.FromPoint(WinForms.Cursor.Position);
        var handle = new WindowInteropHelper(this).Handle;
        var dpi = GetDpiForWindow(handle);
        var scale = dpi <= 0 ? 1d : dpi / 96d;
        var width = (int)Math.Round(Width * scale);
        var height = (int)Math.Round(Height * scale);
        var x = screen.Bounds.Left + ((screen.Bounds.Width - width) / 2);
        var y = screen.Bounds.Top + ((screen.Bounds.Height - height) / 2);
        SetWindowPos(handle, HwndTopmost, x, y, width, height, SwpNoActivate | SwpShowWindow);
    }

    private System.Windows.Shapes.Path CreateSectorGeometry(double centerAngle, bool selected)
    {
        const double halfSpan = 22.5d;
        var startAngle = (centerAngle - halfSpan) * Math.PI / 180d;
        var endAngle = (centerAngle + halfSpan) * Math.PI / 180d;
        var outerStart = PointOnCircle(startAngle, OuterRadius);
        var outerEnd = PointOnCircle(endAngle, OuterRadius);
        var innerEnd = PointOnCircle(endAngle, InnerRadius);
        var innerStart = PointOnCircle(startAngle, InnerRadius);

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
        figure.Segments.Add(new LineSegment(outerEnd, true));
        figure.Segments.Add(new LineSegment(innerEnd, true));
        figure.Segments.Add(new LineSegment(innerStart, true));
        var geometry = new PathGeometry([figure]);

        return new System.Windows.Shapes.Path
        {
            Data = geometry,
            Fill = selected
                ? new SolidColorBrush(Color.FromArgb(238, 45, 125, 255))
                : new SolidColorBrush(Color.FromArgb(218, 26, 36, 51)),
            Stroke = new SolidColorBrush(Color.FromArgb(selected ? (byte)245 : (byte)105, 255, 255, 255)),
            StrokeThickness = selected ? 2.2d : 1d,
            IsHitTestVisible = false
        };
    }

    private FrameworkElement CreateGlyphBlock(int directionIndex, double angle, bool selected, int layerIndex, bool shiftActive)
    {
        var radians = angle * Math.PI / 180d;
        var center = PointOnCircle(radians, 166d);
        var container = new Canvas { Width = 98d, Height = 98d };
        AddButtonGlyph(container, slot: 3, x: 49d, y: 15d, selected, directionIndex, layerIndex, shiftActive);
        AddButtonGlyph(container, slot: 2, x: 15d, y: 49d, selected, directionIndex, layerIndex, shiftActive);
        AddButtonGlyph(container, slot: 1, x: 83d, y: 49d, selected, directionIndex, layerIndex, shiftActive);
        AddButtonGlyph(container, slot: 0, x: 49d, y: 83d, selected, directionIndex, layerIndex, shiftActive);
        Canvas.SetLeft(container, center.X - (container.Width / 2d));
        Canvas.SetTop(container, center.Y - (container.Height / 2d));
        return container;
    }

    private void AddButtonGlyph(
        Canvas canvas,
        int slot,
        double x,
        double y,
        bool selected,
        int directionIndex,
        int layerIndex,
        bool shiftActive)
    {
        const double diameter = 30d;
        var glyph = TypingWheelLayout.DisplayLabel(layerIndex, (TypingWheelDirection)directionIndex, slot, shiftActive);
        var circle = new Border
        {
            Width = diameter,
            Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2d),
            Background = selected ? _buttonBrushes[slot] : Brushes.Transparent,
            BorderBrush = selected
                ? new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))
                : Brushes.Transparent,
            BorderThickness = new Thickness(0.6d),
            Child = new TextBlock
            {
                Text = glyph,
                Foreground = Brushes.White,
                FontSize = glyph.Length > 1 ? 11 : 17,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Canvas.SetLeft(circle, x - (diameter / 2d));
        Canvas.SetTop(circle, y - (diameter / 2d));
        canvas.Children.Add(circle);
    }

    private static Point PointOnCircle(double angle, double radius) =>
        new(Center + (Math.Cos(angle) * radius), Center - (Math.Sin(angle) * radius));

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style | WsExTransparent | WsExToolWindow | WsExNoActivate));
    }

    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : new IntPtr(GetWindowLong32(window, index));

    private static void SetWindowLongPtr(IntPtr window, int index, IntPtr value)
    {
        if (IntPtr.Size == 8) _ = SetWindowLongPtr64(window, index, value);
        else _ = SetWindowLong32(window, index, value.ToInt32());
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr window, int index, int value);
}
