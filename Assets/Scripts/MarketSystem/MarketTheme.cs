using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Voidhaul Quartermaster aesthetic — kurumsal soguk-celik kiosk, cyan brand,
/// extraction void'inde yuzen terminal. Tum market UI renkleri tek kaynak.
/// </summary>
public static class MarketTheme
{
    // Backgrounds
    public static readonly Color Backdrop    = new(0.020f, 0.030f, 0.060f, 0.85f);
    public static readonly Color PanelBg     = new(0.055f, 0.075f, 0.115f, 0.98f);
    public static readonly Color PanelDeep   = new(0.028f, 0.040f, 0.068f, 1.00f);
    public static readonly Color HeaderBar   = new(0.040f, 0.058f, 0.092f, 1.00f);
    public static readonly Color FooterBar   = new(0.035f, 0.050f, 0.082f, 1.00f);
    public static readonly Color IconBg      = new(0.025f, 0.038f, 0.062f, 1.00f);

    // Rows
    public static readonly Color RowIdle     = new(0.085f, 0.115f, 0.155f, 0.95f);
    public static readonly Color RowAlt      = new(0.075f, 0.100f, 0.140f, 0.95f);

    // Dividers/borders
    public static readonly Color Divider     = new(0.00f,  0.90f, 1.00f, 0.22f);
    public static readonly Color DividerSoft = new(0.00f,  0.90f, 1.00f, 0.08f);

    // Text
    public static readonly Color TextPrimary = new(0.82f, 0.88f, 0.92f, 1f);
    public static readonly Color TextDim     = new(0.42f, 0.50f, 0.58f, 1f);
    public static readonly Color TextMuted   = new(0.28f, 0.34f, 0.40f, 1f);

    // Brand cyan — BUY / credits / accents
    public static readonly Color Accent      = new(0.00f, 0.90f, 1.00f, 1f);
    public static readonly Color AccentDim   = new(0.00f, 0.55f, 0.65f, 1f);
    public static readonly Color AccentBtnHi = new(0.20f, 0.95f, 1.00f, 1f);

    // SELL amber
    public static readonly Color Sell        = new(1.00f, 0.65f, 0.20f, 1f);
    public static readonly Color SellDim     = new(0.65f, 0.42f, 0.13f, 1f);
    public static readonly Color SellBtnHi   = new(1.00f, 0.75f, 0.30f, 1f);

    // LIQUIDATE — toplu sat, dikkatli ton
    public static readonly Color Liquidate   = new(0.92f, 0.32f, 0.28f, 1f);
    public static readonly Color LiquidateHi = new(1.00f, 0.42f, 0.36f, 1f);

    // Status feedback
    public static readonly Color Success     = new(0.30f, 0.85f, 0.50f, 1f);
    public static readonly Color Warning     = new(1.00f, 0.65f, 0.20f, 1f);
    public static readonly Color Error       = new(0.96f, 0.30f, 0.27f, 1f);

    // Disabled / locked
    public static readonly Color Inert       = new(0.18f, 0.22f, 0.27f, 1f);
    public static readonly Color InertText   = new(0.40f, 0.46f, 0.52f, 1f);

    public static ColorBlock AccentButton() => MakeBlock(Accent, AccentBtnHi);
    public static ColorBlock SellButton()   => MakeBlock(Sell,   SellBtnHi);
    public static ColorBlock DangerButton() => MakeBlock(Liquidate, LiquidateHi);
    public static ColorBlock IconButton()   => MakeBlock(HeaderBar, RowIdle);

    private static ColorBlock MakeBlock(Color normal, Color hi)
    {
        return new ColorBlock
        {
            normalColor      = normal,
            highlightedColor = hi,
            pressedColor     = new Color(normal.r * 0.7f, normal.g * 0.7f, normal.b * 0.7f, normal.a),
            selectedColor    = hi,
            disabledColor    = Inert,
            colorMultiplier  = 1f,
            fadeDuration     = 0.12f
        };
    }
}

