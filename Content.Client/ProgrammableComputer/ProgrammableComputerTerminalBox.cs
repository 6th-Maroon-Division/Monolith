using System;
using System.Numerics;
using System.Text;
using Content.Client.Stylesheets;
using Content.Shared.ProgrammableComputer;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.ProgrammableComputer;

/// <summary>
/// Dedicated fixed-grid terminal surface for programmable computers.
/// This is intentionally a custom renderer so we can add per-cell color, graphics and touch input later.
/// </summary>
public sealed class ProgrammableComputerTerminalBox : Control
{
    private const string FontPath = "/Fonts/Minecraftia.ttf";
    private const int FontSize = 10;
    private const float Padding = 8f;
    private const float CursorBlinkInterval = 0.5f;
    private static readonly Color DefaultForeground = Color.FromHex("#9cffad");
    private static readonly Color DefaultBackground = Color.FromHex("#070a0c");

    private readonly Font _font;
    private ProgrammableComputerTerminalCell[] _cells;

    private readonly float _cellWidth;
    private readonly float _cellHeight;
    private float _drawCellWidth;
    private float _drawCellHeight;

    private int _cursorX = 1;
    private int _cursorY = 1;
    private bool _cursorBlinkEnabled;
    private bool _cursorVisible = true;
    private float _cursorBlinkAccumulator;

    public event Action<Vector2i>? OnCellPressed;
    public event Action? OnTerminalClicked;
    public event Action<string>? OnTerminalTextEntered;

    public int Columns { get; private set; } = ProgrammableComputerComponent.TerminalWidth;
    public int Rows { get; private set; } = ProgrammableComputerComponent.TerminalHeight;

    public ProgrammableComputerTerminalBox()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.NotoStack2ElectricBoogaloo(FontPath, FontSize);

        _cells = new ProgrammableComputerTerminalCell[Rows * Columns];
        FillBlankCells();

        _cellWidth = _font.GetCharMetrics(new Rune('W'), 1f)?.Advance ?? 8f;
        _cellHeight = _font.GetLineHeight(1f);
        _drawCellWidth = _cellWidth;
        _drawCellHeight = _cellHeight;

        RecalculateTerminalBounds();

        CanKeyboardFocus = true;
        KeyboardFocusOnClick = true;
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        RectDrawClipMargin = 0;
    }

    public void UpdateTerminal(ProgrammableComputerTerminalCell[] cells, int columns, int rows, int cursorX, int cursorY, bool cursorBlink)
    {
        EnsureGridSize(columns, rows);

        for (var index = 0; index < _cells.Length; index++)
        {
            if (index < cells.Length)
            {
                _cells[index] = cells[index];
                continue;
            }

            _cells[index] = new ProgrammableComputerTerminalCell('\0', DefaultForeground, DefaultBackground);
        }

        _cursorX = Math.Clamp(cursorX, 1, Columns);
        _cursorY = Math.Clamp(cursorY, 1, Rows);
        _cursorBlinkEnabled = cursorBlink;

        if (!_cursorBlinkEnabled)
        {
            _cursorVisible = false;
            _cursorBlinkAccumulator = 0f;
            return;
        }

        _cursorVisible = true;
        _cursorBlinkAccumulator = 0f;
    }

    private void EnsureGridSize(int columns, int rows)
    {
        var targetColumns = Math.Max(1, columns);
        var targetRows = Math.Max(1, rows);
        if (targetColumns == Columns && targetRows == Rows)
            return;

        Columns = targetColumns;
        Rows = targetRows;
        _cells = new ProgrammableComputerTerminalCell[Rows * Columns];
        FillBlankCells();
        RecalculateTerminalBounds();
    }

    private void RecalculateTerminalBounds()
    {
        // Round up and keep a small extra gutter so first/last glyphs are never clipped by sub-pixel metrics.
        var width = MathF.Ceiling(Padding * 2f + Columns * _cellWidth + 2f);
        var height = MathF.Ceiling(Padding * 2f + Rows * _cellHeight + 2f);
        MinSize = new Vector2(width, height);
        SetSize = new Vector2(width, height);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_cursorBlinkEnabled)
            return;

        _cursorBlinkAccumulator += args.DeltaSeconds;
        if (_cursorBlinkAccumulator < CursorBlinkInterval)
            return;

        _cursorBlinkAccumulator -= CursorBlinkInterval;
        _cursorVisible = !_cursorVisible;
    }

    private void FillBlankCells()
    {
        for (var index = 0; index < _cells.Length; index++)
        {
            _cells[index] = new ProgrammableComputerTerminalCell('\0', DefaultForeground, DefaultBackground);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        handle.DrawRect(PixelSizeBox, DefaultBackground);

        var availableWidth = MathF.Max(1f, PixelSizeBox.Width - Padding * 2f - 2f);
        var availableHeight = MathF.Max(1f, PixelSizeBox.Height - Padding * 2f - 2f);
        var scaleX = availableWidth / (Columns * _cellWidth);
        var scaleY = availableHeight / (Rows * _cellHeight);
        var fontScale = MathF.Max(0.1f, MathF.Min(scaleX, scaleY));

        _drawCellWidth = _cellWidth * fontScale;
        _drawCellHeight = _cellHeight * fontScale;

        var drawWidth = Columns * _drawCellWidth;
        var drawHeight = Rows * _drawCellHeight;
        var offsetX = Padding + MathF.Max(0f, (availableWidth - drawWidth) * 0.5f);
        var offsetY = Padding + MathF.Max(0f, (availableHeight - drawHeight) * 0.5f);
        var pos = PixelSizeBox.TopLeft + new Vector2(offsetX, offsetY);
        var cursorIndex = (_cursorY - 1) * Columns + (_cursorX - 1);

        for (var row = 0; row < Rows; row++)
        {
            var rowOffset = pos + new Vector2(0f, row * _drawCellHeight);

            for (var col = 0; col < Columns; col++)
            {
                var cellIndex = row * Columns + col;
                var cell = _cells[cellIndex];
                var cellPos = rowOffset + new Vector2(col * _drawCellWidth, 0f);
                var cellRect = UIBox2.FromDimensions(cellPos, new Vector2(_drawCellWidth, _drawCellHeight));
                var isCursorCell = _cursorVisible && cellIndex == cursorIndex;
                var cursorColor = cell.Foreground == Color.Transparent ? DefaultForeground : cell.Foreground;
                var backgroundColor = isCursorCell ? cursorColor : cell.Background;

                if (backgroundColor != DefaultBackground || isCursorCell)
                    handle.DrawRect(cellRect, backgroundColor);

                if (cell.Glyph == '\0' || cell.Glyph == ' ')
                    continue;

                var glyphPos = cellPos;
                if (_font.TryGetCharMetrics(new Rune(cell.Glyph), 1f, out var metrics))
                {
                    var glyphWidth = metrics.Width * fontScale;
                    var glyphOffsetX = MathF.Max(0f, (_drawCellWidth - glyphWidth) * 0.5f);
                    glyphPos += new Vector2(glyphOffsetX, 0f);
                }

                var glyphColor = isCursorCell
                    ? (cell.Background == Color.Transparent ? DefaultBackground : cell.Background)
                    : cell.Foreground;

                handle.DrawString(_font, glyphPos, cell.Glyph.ToString(), fontScale, glyphColor);
            }
        }
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        OnTerminalClicked?.Invoke();

        var local = args.RelativePixelPosition;
        var cellX = (int) MathF.Floor((local.X - Padding) / _drawCellWidth) + 1;
        var cellY = (int) MathF.Floor((local.Y - Padding) / _drawCellHeight) + 1;

        if (cellX < 1 || cellX > Columns || cellY < 1 || cellY > Rows)
            return;

        OnCellPressed?.Invoke(new Vector2i(cellX, cellY));
        args.Handle();
    }

    protected override void TextEntered(GUITextEnteredEventArgs args)
    {
        base.TextEntered(args);

        if (string.IsNullOrEmpty(args.Text))
            return;

        OnTerminalTextEntered?.Invoke(args.Text);
    }
}
