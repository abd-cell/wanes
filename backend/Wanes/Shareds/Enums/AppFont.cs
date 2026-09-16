namespace Wanes.Shareds.Enums;

/// <summary>
/// The typeface the admin picks for the platform.
///
/// A closed set rather than a free-text family name, for three reasons: the app
/// resolves each option to a concrete `google_fonts` face at compile time (a
/// name typed by an admin could not be verified, and a miss would leave the app
/// with no type at all), the CMS has to load a matching webfont, and — the one
/// that really forces it — every option has to name *two* faces, because a Latin
/// display face carries no Arabic glyphs and Wanes runs in both scripts. Each
/// value below is therefore a pairing, not a font.
///
/// Only the display/body face is configurable. The mono/data face (JetBrains
/// Mono, IBM Plex Sans Arabic in Arabic) stays fixed: it marks a numeric or
/// label role rather than carrying the brand, and swapping it per install would
/// break the alignment those readings depend on.
/// </summary>
public enum AppFont
{
    /// <summary>Plus Jakarta Sans + Cairo — the shipped design.</summary>
    Jakarta = 1,

    /// <summary>Inter + IBM Plex Sans Arabic. Neutral, dense UI type.</summary>
    Inter = 2,

    /// <summary>Rubik, both scripts from one family — rounder, friendlier.</summary>
    Rubik = 3,

    /// <summary>Noto Sans + Noto Sans Arabic. The widest glyph coverage.</summary>
    Noto = 4,

    /// <summary>Tajawal, both scripts — Arabic-first, for an Arabic-primary market.</summary>
    Tajawal = 5,

    /// <summary>
    /// Whatever the device or browser uses for its own UI. No webfont is
    /// downloaded, so first paint carries no type-loading cost at all.
    /// </summary>
    System = 6,

    /// <summary>Almarai, both scripts — Arabic-first geometric, no 600 weight.</summary>
    Almarai = 7,

    /// <summary>Readex Pro, both scripts. Drawn for Latin and Arabic together.</summary>
    ReadexPro = 8,

    /// <summary>Alexandria, both scripts — geometric, wide weight range.</summary>
    Alexandria = 9,

    /// <summary>Poppins + Almarai. Round geometric Latin display.</summary>
    Poppins = 10,

    /// <summary>Montserrat + El Messiri. Wide Latin, Arabic with matching flair.</summary>
    Montserrat = 11,

    /// <summary>Amiri, both scripts — the only serif in the set, editorial.</summary>
    Amiri = 12,
}
