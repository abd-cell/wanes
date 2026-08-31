/**
 * Brand shade derivation.
 *
 * The admin picks one colour; every other brand token is computed from it so a
 * re-skin can never leave a hover state or a button foreground on the old hue.
 * The maths is HSL: the hue and saturation carry over untouched and only the
 * lightness moves, which keeps derived shades recognisably the same colour.
 *
 * The default #0FAE9E reproduces the shipped palette almost exactly, so a fresh
 * install looks identical to the hand-tuned design it replaces.
 */
export interface BrandShades {
  /** As configured. */
  primary: string;
  /** Darker: hover fills, and link/label text on a light background. */
  primaryDeep: string;
  /** Lifted for the dark theme, where the configured shade would sit too close to the surface. */
  darkPrimary: string;
  darkPrimaryDeep: string;
  /** Whatever reads on a solid `primary` fill — near-black or near-white, whichever wins on contrast. */
  onPrimary: string;
}

const clamp01 = (n: number): number => Math.min(1, Math.max(0, n));

/** `#abc` / `abc123` / `#AABBCC` → `[r, g, b]` in 0–255. Invalid input falls back to the brand teal. */
export function hexToRgb(hex: string): [number, number, number] {
  let h = (hex ?? '').trim().replace('#', '');
  if (h.length === 3) h = h.split('').map((c) => c + c).join('');
  if (!/^[0-9a-fA-F]{6}$/.test(h)) h = '0FAE9E';
  return [
    parseInt(h.slice(0, 2), 16),
    parseInt(h.slice(2, 4), 16),
    parseInt(h.slice(4, 6), 16),
  ];
}

function rgbToHsl(r: number, g: number, b: number): [number, number, number] {
  const [rn, gn, bn] = [r / 255, g / 255, b / 255];
  const max = Math.max(rn, gn, bn);
  const min = Math.min(rn, gn, bn);
  const l = (max + min) / 2;
  if (max === min) return [0, 0, l]; // achromatic — hue is meaningless

  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
  const h = max === rn
    ? ((gn - bn) / d + (gn < bn ? 6 : 0))
    : max === gn
      ? (bn - rn) / d + 2
      : (rn - gn) / d + 4;
  return [h / 6, s, l];
}

function hslToHex(h: number, s: number, l: number): string {
  const f = (n: number): number => {
    const k = (n + h * 12) % 12;
    const a = s * Math.min(l, 1 - l);
    return l - a * Math.max(-1, Math.min(k - 3, 9 - k, 1));
  };
  const to = (v: number): string =>
    Math.round(clamp01(v) * 255).toString(16).padStart(2, '0');
  return `#${to(f(0))}${to(f(8))}${to(f(4))}`;
}

/**
 * WCAG relative luminance. Used only to decide black-vs-white text: above the
 * 0.179 crossover a near-black foreground has the better contrast ratio.
 */
export function relativeLuminance(hex: string): number {
  const [r, g, b] = hexToRgb(hex).map((v) => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function deriveBrand(primary: string): BrandShades {
  const [r, g, b] = hexToRgb(primary);
  const [h, s, l] = rgbToHsl(r, g, b);

  // A very dark or very light brand colour has no headroom left to darken, so
  // the dark-theme shade is pinned to a floor rather than scaled.
  const darkL = Math.max(l, 0.46);
  const light = relativeLuminance(primary) <= 0.179;

  return {
    primary: hslToHex(h, s, l),
    primaryDeep: hslToHex(h, s, clamp01(l * 0.75)),
    darkPrimary: hslToHex(h, s, darkL),
    darkPrimaryDeep: hslToHex(h, s, clamp01(darkL * 0.85)),
    // Tinted rather than pure #000/#fff: a trace of the brand hue keeps the
    // label from looking like a foreign colour sitting on the fill.
    onPrimary: light ? hslToHex(h, Math.min(s, 0.35), 0.97) : hslToHex(h, Math.min(s, 0.85), 0.08),
  };
}
