import { AppFont } from '../api/models';

/**
 * What each configured font resolves to in the browser.
 *
 * The admin picks one value; this table turns it into the two things the CMS
 * needs — a CSS stack to set on the document, and the Google Fonts request that
 * makes that stack resolvable. The stack always ends in the same system
 * fallbacks, so type still renders while the webfont is in flight, or forever
 * if the network never produces it.
 *
 * Latin and Arabic are separate entries because a Latin display face carries no
 * Arabic glyphs. Listing both in one stack is what makes a mixed page work: the
 * browser takes each glyph from the first family in the stack that has it, so
 * Arabic text falls through the Latin face to the Arabic one on its own — no
 * direction-aware rule needed for the shared case. `[dir='rtl']` still leads
 * with the Arabic face, so Arabic digits and punctuation come from the family
 * that shapes them alongside the script.
 */
export interface FontChoice {
  /** Face for Latin text, and the head of the LTR stack. */
  latin: string;
  /** Face for Arabic text, and the head of the RTL stack. */
  arabic: string;
  /**
   * `family=` segments for the Google Fonts v2 API — one per distinct face,
   * already weight-scoped. Empty for [[AppFont.System]], which loads nothing.
   */
  google: string[];
}

/** The system stack every option falls back through, and all [[AppFont.System]] uses. */
const SYSTEM_STACK = `'Segoe UI', Tahoma, system-ui, -apple-system, sans-serif`;

/**
 * The weights the CMS actually renders (400 body, 600/700 labels and headings).
 * Narrowing the request keeps the download to what is used rather than pulling
 * a family's full range.
 */
const WEIGHTS = 'wght@400;600;700';

const FONTS: Record<AppFont, FontChoice> = {
  [AppFont.Jakarta]: {
    latin: `'Plus Jakarta Sans'`,
    arabic: `'Cairo'`,
    google: [`Plus+Jakarta+Sans:${WEIGHTS}`, `Cairo:${WEIGHTS}`],
  },
  [AppFont.Inter]: {
    latin: `'Inter'`,
    arabic: `'IBM Plex Sans Arabic'`,
    // IBM Plex Sans Arabic is not a variable font, so its weights are listed
    // discretely rather than as a `wght@` range.
    google: [`Inter:${WEIGHTS}`, `IBM+Plex+Sans+Arabic:wght@400;600;700`],
  },
  [AppFont.Rubik]: {
    latin: `'Rubik'`,
    arabic: `'Rubik'`,
    google: [`Rubik:${WEIGHTS}`],
  },
  [AppFont.Noto]: {
    latin: `'Noto Sans'`,
    arabic: `'Noto Sans Arabic'`,
    google: [`Noto+Sans:${WEIGHTS}`, `Noto+Sans+Arabic:${WEIGHTS}`],
  },
  [AppFont.Tajawal]: {
    latin: `'Tajawal'`,
    arabic: `'Tajawal'`,
    google: [`Tajawal:wght@400;500;700`],
  },
  [AppFont.System]: { latin: '', arabic: '', google: [] },
  [AppFont.Almarai]: {
    latin: `'Almarai'`,
    arabic: `'Almarai'`,
    // Almarai ships 300/400/700/800 — asking for a 600 it does not have makes
    // the whole css2 request a 400, and the page would then get no face at all.
    google: [`Almarai:wght@400;700`],
  },
  [AppFont.ReadexPro]: {
    latin: `'Readex Pro'`,
    arabic: `'Readex Pro'`,
    google: [`Readex+Pro:${WEIGHTS}`],
  },
  [AppFont.Alexandria]: {
    latin: `'Alexandria'`,
    arabic: `'Alexandria'`,
    google: [`Alexandria:${WEIGHTS}`],
  },
  [AppFont.Poppins]: {
    latin: `'Poppins'`,
    arabic: `'Almarai'`,
    google: [`Poppins:wght@400;600;700`, `Almarai:wght@400;700`],
  },
  [AppFont.Montserrat]: {
    latin: `'Montserrat'`,
    arabic: `'El Messiri'`,
    google: [`Montserrat:${WEIGHTS}`, `El+Messiri:${WEIGHTS}`],
  },
  [AppFont.Amiri]: {
    latin: `'Amiri'`,
    arabic: `'Amiri'`,
    // A serif, and a static family: 400 and 700 only.
    google: [`Amiri:wght@400;700`],
  },
};

/**
 * The table entry for a value, with an unknown one — a row written by a build
 * that knew a font this one does not — falling back to the default pairing.
 */
export function fontChoice(font: AppFont | number | null | undefined): FontChoice {
  return FONTS[font as AppFont] ?? FONTS[AppFont.Jakarta];
}

/**
 * Joins the faces into a stack, dropping blanks and repeats — the options where
 * one family covers both scripts would otherwise name it twice.
 */
function stack(...faces: string[]): string {
  return [...new Set(faces.filter(Boolean)), SYSTEM_STACK].join(', ');
}

/** `font-family` for LTR text: the Latin face first, Arabic behind it for stray Arabic. */
export function fontStack(font: AppFont | number | null | undefined): string {
  const c = fontChoice(font);
  return stack(c.latin, c.arabic);
}

/** `font-family` for RTL text: the same faces, Arabic first. */
export function rtlFontStack(font: AppFont | number | null | undefined): string {
  const c = fontChoice(font);
  return stack(c.arabic, c.latin);
}

/**
 * The stylesheet URL that makes the stack resolvable, or null for a font that
 * needs no download. `display=swap` is deliberate: the page paints in the
 * fallback immediately and swaps when the face lands, rather than holding text
 * blank while a font server is slow.
 */
export function fontHref(font: AppFont | number | null | undefined): string | null {
  const { google } = fontChoice(font);
  if (!google.length) return null;
  return `https://fonts.googleapis.com/css2?${google.map((f) => `family=${f}`).join('&')}&display=swap`;
}

/**
 * Puts the `<link>` for `font` in the document under `elementId`, or takes it
 * away again for a font that needs no download.
 *
 * Keyed by id and re-pointed rather than re-created, so switching fonts
 * repeatedly leaves one request rather than a queue of them. Two callers want
 * this with different ids: the config service, applying the saved font, and the
 * settings screen, showing a font the admin is only considering — and the
 * second must not disturb the first.
 */
export function ensureFontStylesheet(
  doc: Document,
  font: AppFont | number | null | undefined,
  elementId: string,
): void {
  const href = fontHref(font);
  const existing = doc.getElementById(elementId) as HTMLLinkElement | null;

  if (!href) {
    existing?.remove();
    return;
  }

  const link = existing ?? doc.createElement('link');
  if (!existing) {
    link.id = elementId;
    link.rel = 'stylesheet';
    doc.head.appendChild(link);
  }
  if (link.href !== href) link.href = href;
}
