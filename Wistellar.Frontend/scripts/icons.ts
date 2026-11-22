import { chromium } from "@playwright/test";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

// Draws the app icons into static/ from the Wistellar mark: the favicon, the web app manifest's
// icons, the iOS home screen icon, an animated SVG of the mark, and the startup screen's mark and
// waves (the generated rules in static/splash.css, the rings in src/app.html). The outputs are
// committed, so only run this (`pnpm run icons`) after changing the mark. It renders with the
// installed Chrome, like the e2e tests.

const ground = "#070a12";
const silver = "#e9eef8";
const wave = "#5aa2ff"; // the map's wifi layer color, the only accent

// A "W" drawn as a constellation of signal points, its middle peak a bright transmitter sending
// rings of signal outward. The rings are cut away around the W's lines and points and inside its
// lower V, so they read as passing behind it, and fade out above its lower points, so nothing of
// them shows below it. 32×32 units; every output centers the source, so the rings start in the
// middle of the icon.
const w = "M4 9 L11 23 L16 13 L21 23 L28 9";
const points: [number, number][] = [[4, 9], [11, 23], [21, 23], [28, 9]];
const source = { x: 16, y: 13, r: 2.4 };
const rings = [
    { r: 5.4, opacity: 0.7 },
    { r: 8.9, opacity: 0.38 },
    { r: 12.4, opacity: 0.16 },
];
const ringWidth = 1.3;
const pointHole = 3.2;
const sourceHole = source.r + 1.8;
/** The animated mark's and the startup screen's viewBox: 34 units square, centered on the source. */
const view = { x: source.x - 17, y: source.y - 17, size: 34 };
const viewBox = `${view.x} ${view.y} ${view.size} ${view.size}`;
const under = { from: 19.5, to: 22.5 }; // the rings fade out between these heights

/** A vertical gradient from transparent `color` at `under.from` to opaque at `under.to`. */
const underGradient = (id: string, color: string) => `
    <linearGradient id="${id}" gradientUnits="userSpaceOnUse" x1="0" y1="${under.from}" x2="0" y2="${under.to}">
      <stop offset="0" stop-color="${color}" stop-opacity="0"/>
      <stop offset="1" stop-color="${color}"/>
    </linearGradient>`;

const markDefs = `
    <radialGradient id="glow">
      <stop offset="0" stop-color="#7fb4ff" stop-opacity="0.6"/>
      <stop offset="1" stop-color="#7fb4ff" stop-opacity="0"/>
    </radialGradient>${underGradient("under", "#000")}
    <mask id="behind" maskUnits="userSpaceOnUse" x="-8" y="-8" width="48" height="48">
      <rect x="-8" y="-8" width="48" height="48" fill="#fff"/>
      <rect x="-8" y="${under.from}" width="48" height="${40 - under.from}" fill="url(#under)"/>
      <path d="${w}" fill="none" stroke="#000" stroke-width="4.8" stroke-linejoin="round" stroke-linecap="round"/>
      <path d="M11 23 L16 13 L21 23 Z" fill="#000"/>${points.map(([x, y]) => `
      <circle cx="${x}" cy="${y}" r="${pointHole}" fill="#000"/>`).join("")}
      <circle cx="${source.x}" cy="${source.y}" r="${sourceHole}" fill="#000"/>
    </mask>`;

const stillRings = rings.map(({ r, opacity }) => `
      <circle cx="${source.x}" cy="${source.y}" r="${r}" stroke-opacity="${opacity}"/>`).join("");

/**
 * The mark with `rings` behind the W, cut by the mask. `under` goes between them and the W, uncut,
 * and `glow` animates the source's glow.
 */
function mark(rings: string, { under = "", glow = "" } = {}) {
    return `
    <g mask="url(#behind)" fill="none" stroke="${wave}" stroke-width="${ringWidth}">${rings}
    </g>${under}
    <path d="${w}" fill="none" stroke="${silver}" stroke-opacity="0.92" stroke-width="1.8"
          stroke-linejoin="round" stroke-linecap="round"/>${points.map(([x, y]) => `
    <circle cx="${x}" cy="${y}" r="1.75" fill="${silver}"/>`).join("")}
    <circle cx="${source.x}" cy="${source.y}" r="${source.r * 2.4}" fill="url(#glow)">${glow}</circle>
    <circle cx="${source.x}" cy="${source.y}" r="${source.r}" fill="#fff"/>`;
}

/**
 * The mark on a night-sky tile. `any` is a rounded tile for browsers and desktops to show as is.
 * `full` fills the square for platforms that cut their own shape out of it: Android's maskable
 * icons, and iOS, which fills transparent corners with black. Its mark stays inside the central
 * circle (80% of the width) that every mask keeps.
 */
function icon(shape: "any" | "full") {
    const radius = shape === "any" ? 7 : 0;
    const scale = shape === "any" ? 0.82 : 0.74;
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
  <defs>
    <radialGradient id="sky" cx="50%" cy="38%" r="78%">
      <stop offset="0" stop-color="#131a29"/>
      <stop offset="1" stop-color="${ground}"/>
    </radialGradient>${markDefs}
  </defs>
  <rect width="32" height="32" rx="${radius}" fill="url(#sky)"/>
  <g transform="translate(16 16) scale(${scale}) translate(${-source.x} ${-source.y})">${mark(stillRings)}
  </g>
</svg>
`;
}

// The animated mark: a ring leaves the source every `interval` seconds and fades out as it slows,
// and each point flashes as a ring reaches it. With reduced motion it shows the still rings.
const interval = 1;
const period = rings.length * interval;
const ring = { from: sourceHole, to: 14, spline: [0.3, 0.55, 0.5, 1] };
/** A ring fades in to `peak` over the first `at` of its period, then out until it ends. */
const waveOpacity = { peak: 0.85, at: 0.08 };

/** The fraction of a ring's period at which its eased radius reaches `r`. */
function timeAtRadius(r: number) {
    const [x1, y1, x2, y2] = ring.spline;
    const bezier = (a: number, b: number, s: number) => 3 * a * s * (1 - s) ** 2 + 3 * b * s * s * (1 - s) + s ** 3;
    const progress = (r - ring.from) / (ring.to - ring.from);
    let low = 0;
    let high = 1;
    for (let i = 0; i < 40; i++) {
        const s = (low + high) / 2;
        if (bezier(y1, y2, s) < progress) low = s;
        else high = s;
    }
    return bezier(x1, x2, (low + high) / 2);
}

const seconds = (s: number) => `${+s.toFixed(3)}s`;
const repeat = `dur="${seconds(period)}" repeatCount="indefinite"`;

// Negative begins start every ring part-way through its period, so the first frame already has all
// of them out.
const movingRings = rings.map((_, i) => `
      <circle cx="${source.x}" cy="${source.y}" r="${ring.from}" stroke-opacity="0">
        <animate attributeName="r" values="${ring.from};${ring.to}" keyTimes="0;1" calcMode="spline"
                 keySplines="${ring.spline.join(" ")}" begin="${seconds(-i * interval)}" ${repeat}/>
        <animate attributeName="stroke-opacity" values="0;${waveOpacity.peak};0" keyTimes="0;${waveOpacity.at};1"
                 begin="${seconds(-i * interval)}" ${repeat}/>
      </circle>`).join("");

// A ring reaches a point every `interval` seconds, at the same offset each time. The flash stays
// inside the gap the mask cuts around the point.
const pings = points.map(([x, y]) => {
    const reached = timeAtRadius(Math.hypot(x - source.x, y - source.y) - 1) * period;
    const begin = seconds((reached % interval) - interval);
    const timing = `keyTimes="0;0.1;0.55;1" begin="${begin}" dur="${seconds(interval)}" repeatCount="indefinite"`;
    return `
      <circle cx="${x}" cy="${y}" r="1.75" stroke-opacity="0">
        <animate attributeName="r" values="1.75;2.3;${pointHole - 0.45};${pointHole - 0.45}" ${timing}/>
        <animate attributeName="stroke-opacity" values="0;0.75;0;0" ${timing}/>
      </circle>`;
}).join("");

const breathing = `
      <animate attributeName="opacity" values="1;0.5;1" keyTimes="0;0.5;1" calcMode="spline"
               keySplines="0.4 0 0.6 1;0.4 0 0.6 1" dur="${seconds(interval)}" repeatCount="indefinite"/>`;

/** The animated mark without a tile, for the startup screen's dark background. */
function animated() {
    // The pings sit on the points, where the mask cuts the rings away.
    const under = `
    <g fill="none" stroke="${wave}" stroke-width="0.9">${pings}
    </g>`;
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}">
  <style>
    .still { display: none; }
    @media (prefers-reduced-motion: reduce) {
      .moving { display: none; }
      .still { display: inline; }
    }
  </style>
  <defs>${markDefs}
  </defs>
  <g class="moving">${mark(movingRings, { under, glow: breathing })}
  </g>
  <g class="still">${mark(stillRings)}
  </g>
</svg>
`;
}

/**
 * The still parts of the startup screen's mark: the W and its points, over the gaps that the
 * mask cuts in the rings elsewhere, filled with the screen's background color. static/splash.css
 * animates the rings, the flashes and the glow behind and around it with CSS, which keeps running
 * while the app's start keeps the main thread busy, unlike SMIL. Same viewBox as the animated mark.
 */
function splashMark() {
    const gap = "#0b1020"; // the middle of the startup screen's background, in static/splash.css
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}">
  <defs>${underGradient("under", gap)}
  </defs>
  <rect x="${view.x}" y="${under.from}" width="${view.size}" height="${view.y + view.size - under.from}" fill="url(#under)"/>
  <path d="${w}" fill="none" stroke="${gap}" stroke-width="4.8" stroke-linejoin="round" stroke-linecap="round"/>
  <path d="M11 23 L16 13 L21 23 Z" fill="${gap}"/>${points.map(([x, y]) => `
  <circle cx="${x}" cy="${y}" r="${pointHole}" fill="${gap}"/>`).join("")}
  <path d="${w}" fill="none" stroke="${silver}" stroke-opacity="0.92" stroke-width="1.8"
        stroke-linejoin="round" stroke-linecap="round"/>${points.map(([x, y]) => `
  <circle cx="${x}" cy="${y}" r="1.75" fill="${silver}"/>`).join("")}
</svg>
`;
}

/**
 * Writes `content` between the `begin` and `end` markers in `file`, relative to this script, with
 * the file's own line endings (an editor or git may have turned them into CRLF).
 */
function replaceGenerated(file: string, begin: string, end: string, content: string) {
    const path = fileURLToPath(new URL(file, import.meta.url));
    const text = readFileSync(path, "utf8");
    const from = text.indexOf(begin);
    const to = text.indexOf(end);
    if (from < 0 || to < from) throw new Error(`${file} has no "${begin}" … "${end}" markers`);
    const eol = text.includes("\r\n") ? "\r\n" : "\n";
    writeFileSync(path, text.slice(0, from + begin.length) + content.replaceAll("\n", eol) + text.slice(to));
}

const em = (n: number) => `${+n.toFixed(3)}em`;
const percent = (n: number) => `${+n.toFixed(2)}%`;

/**
 * The startup screen's moving parts, positioned in units of the mark's viewBox (one em in
 * static/splash.css). CSS can only move them with transform and opacity, and scaling a ring would
 * scale its line, so a wave is a row of still rings `waveStep` apart, each lighting up while the
 * wave's eased front passes its radius and handing over to the next, so that one of them is
 * always fully lit. The radii include the still rings', which show with reduced motion.
 */
function splashGeometry() {
    const at = (x: number, y: number) => `left: ${em(x - view.x)}; top: ${em(y - view.y)};`;
    const circle = (r: number) => `width: ${em(2 * r)}; height: ${em(2 * r)}; margin: ${em(-r)};`;
    const waveStep = 0.35;
    const first = rings[0].r - waveStep * Math.floor((rings[0].r - ring.from) / waveStep);
    const radii = Array.from({ length: Math.floor((ring.to - first) / waveStep) + 1 }, (_, i) => first + i * waveStep);
    // Seconds into a ring's period at which its front reaches r.
    const reaches = (r: number) => timeAtRadius(Math.min(ring.to, Math.max(ring.from, r))) * period;
    const strength = (t: number) =>
        waveOpacity.peak * (t < waveOpacity.at ? t / waveOpacity.at : (1 - t) / (1 - waveOpacity.at));

    const css: string[] = [
        `#splash .waves,\n#splash .glow,\n#splash .core {\n  ${at(source.x, source.y)}\n}`,
        `#splash .glow {\n  ${circle(source.r * 2.4)}\n}`,
        `#splash .core {\n  ${circle(source.r)}\n}`,
        `#splash .ping {\n  ${circle(pointHole)}\n  border-width: 0.9em;\n}`,
        ...points.map(([x, y], i) => {
            const reached = timeAtRadius(Math.hypot(x - source.x, y - source.y) - 1) * period;
            return `#splash .ping.p${i + 1} {\n  ${at(x, y)}\n  animation-delay: ${seconds((reached % interval) - interval)};\n}`;
        }),
        `#splash .waves i::before {\n  border-width: ${em(ringWidth)};\n}`,
    ];
    const still: string[] = [];
    radii.forEach((r, i) => {
        const n = i + 1;
        const [start, rise, fall] = [r - waveStep, r - waveStep / 2, r + waveStep / 2].map(reaches);
        // Each ring lights up once per wave, so it has to be dark again before the next one.
        const end = Math.min(reaches(r + waveStep), start + interval * 0.98);
        const pct = (t: number) => percent(((t - start) / interval) * 100);
        css.push(
            `#splash .waves i:nth-child(${n}) {\n  ${circle(r + ringWidth / 2)}\n  opacity: ${strength(reaches(r) / period).toFixed(3)};\n}`,
            `#splash .waves i:nth-child(${n})::before {\n  animation-name: wave-${n};\n  animation-delay: ${seconds((start % interval) - interval)};\n}`,
            `@keyframes wave-${n} {\n${rise > start ? "  0% {\n    opacity: 0;\n  }\n" : ""}  ${pct(rise)},\n  ${pct(Math.min(fall, end))} {\n    opacity: 1;\n  }\n  ${pct(end)},\n  100% {\n    opacity: 0;\n  }\n}`,
        );
        const match = rings.find((s) => Math.abs(s.r - r) < 1e-6);
        if (match) {
            still.push(
                `  #splash .waves i:nth-child(${n}) {\n    opacity: ${match.opacity};\n  }\n  #splash .waves i:nth-child(${n})::before {\n    opacity: 1;\n  }`,
            );
        }
    });
    css.push(`@media (prefers-reduced-motion: reduce) {\n${still.join("\n")}\n}`);

    replaceGenerated(
        "../static/splash.css",
        "/* Generated by scripts/icons.ts from the mark's geometry. */",
        "/* End of the generated rules. */",
        "\n" + css.join("\n\n") + "\n\n",
    );
    // The rings' markup in src/app.html: one <i> per radius, in the waves element.
    const appHtml = fileURLToPath(new URL("../src/app.html", import.meta.url));
    const waves = /<div class="waves">(?:<i><\/i>)*<\/div>/;
    const html = readFileSync(appHtml, "utf8");
    if (!waves.test(html)) throw new Error('src/app.html has no <div class="waves"> to fill');
    writeFileSync(appHtml, html.replace(waves, `<div class="waves">${"<i></i>".repeat(radii.length)}</div>`));
}

const pngs = [
    { file: "favicon.png", shape: "any", size: 64 },
    { file: "icon-192.png", shape: "any", size: 192 },
    { file: "icon-512.png", shape: "any", size: 512 },
    { file: "icon-maskable-512.png", shape: "full", size: 512 },
    { file: "apple-touch-icon.png", shape: "full", size: 180 },
] as const;

const out = (file: string) => fileURLToPath(new URL(`../static/${file}`, import.meta.url));

writeFileSync(out("icon.svg"), icon("any"));
writeFileSync(out("icon-animated.svg"), animated());
writeFileSync(out("splash-mark.svg"), splashMark());
splashGeometry();

const browser = await chromium.launch({ channel: "chrome" });
try {
    const page = await browser.newPage();
    for (const { file, shape, size } of pngs) {
        await page.setViewportSize({ width: size, height: size });
        const src = `data:image/svg+xml,${encodeURIComponent(icon(shape))}`;
        await page.setContent(
            `<body style="margin:0"><img src="${src}" width="${size}" height="${size}" style="display:block"></body>`,
        );
        await page.locator("img").evaluate((img: HTMLImageElement) => img.decode());
        await page.screenshot({ path: out(file), omitBackground: true });
        console.log(`static/${file}`);
    }
} finally {
    await browser.close();
}
