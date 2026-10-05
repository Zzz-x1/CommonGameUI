# Client asset sources

All resources are local; no runtime downloads. Retrieved / created 2026-10-03.

## Original character and background artwork

`Client/lobby.png`, `Client/event.png`, and `Client/hero0.png` through `hero5.png` were generated for this project with the built-in OpenAI image_gen tool. They are original fictional characters and environments, not screenshots or artwork extracted from an existing game. These files are not labeled CC0 or attributed to a third-party asset author. Transparent alpha is preserved in the six character files.

The exact prompts used follow. Each character was generated in a separate call with `transparent_background: true`; both backgrounds used `false`. Original selected outputs were copied into this project; there is no dependency on a Codex cache path.

**lobby.png**

> Create a polished original fantasy anime mobile RPG lobby environment illustration, wide landscape 16:9 composition, luminous ivory and gold sky city, floating terraces, distant blue mountains, morning light, flowers and elegant stone balustrade, richly painted professional game key art, pastel teal and warm cream palette. Leave center foreground open for a separate character portrait and leave top and bottom visually quiet for UI. No people, no text, no logos, no interface.

**event.png**

> Original fantasy anime game event illustration, wide landscape, magical moonlit botanical garden with luminous blue flowers, crystal observatory and gold celestial instruments, elegant painterly polished mobile RPG key art, teal navy gold palette, no people no words no logos no interface.

For each of the following character descriptions, the complete prompt was:

> Create one original fantasy anime RPG character: {description}. Full body standing three-quarter elegant pose, polished high quality hand-painted anime game character illustration, detailed face, cohesive pastel ivory gold teal fantasy aesthetic. Character fills frame with generous space around feet and hair, no cropping. Transparent background, no environment, no text, no logo, no interface. Adult character.

| File | Exact description |
| --- | --- |
| hero0.png | silver-haired female knight in ivory and gold armor, elegant longsword, blue cape |
| hero1.png | young adult male forest ranger, chestnut hair, green and gold cloak, ornate bow |
| hero2.png | adult female celestial mage, lavender hair, white gold flowing robes, star staff |
| hero3.png | adult male guardian, dark blue hair, teal silver heavy armor and shield |
| hero4.png | adult female alchemist, auburn hair, cream gold dress, satchel and glowing potion |
| hero5.png | adult male bard, blond hair, white green embroidered coat and lyre |

`Client/glow.svg` and `Client/star.svg` are original project vector graphics, authored directly as SVG for radial light and decorative particles. `Themes/Client.tres` is original editable presentation styling; interface text and controls are rendered by Godot, not baked into generated images.

## Chinese font

The Noto Project / Adobe / Google, **Noto Sans CJK SC Regular**.

- Official project: https://github.com/notofonts/noto-cjk
- Original font: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
- License: SIL Open Font License 1.1, original notice retained in `Fonts/Noto-LICENSE.txt`.
- Included file: `Fonts/NotoSansSC.otf`, unchanged font bytes, local filename changed for project references.
- Bundled for Chinese glyph coverage. No runtime or OS font dependency.

## Item icons

Icons made by **Lorc** and **Delapouite**, from https://game-icons.net, licensed under **CC BY 3.0**, https://creativecommons.org/licenses/by/3.0/.

- Official repository: https://github.com/game-icons/icons ; contributor/license notice retained in `Items/License.txt`.
- Delapouite (https://delapouite.com): `bow-arrow`, `diamond-ring`, `belt-armor`, `herbs-bundle`, `sewing-string`, `amber-mosquito`.
- Lorc (https://lorcblog.blogspot.com): `hood`, `boots`, `compass`, `crystal-growth`, `rock`, `dead-wood`.
- Sources: `https://raw.githubusercontent.com/game-icons/icons/master/{author}/{name}.svg`, authors and names listed above.
- Retrieved 2026-10-02 and retained for the rebuilt client.
- Modifications: black background removed, white foreground changed to dark teal `#335653`; geometry and viewBox unchanged.
- In-client attribution: top-left game name opens the credits window.

## Original transition artwork (2026-10-03)

`Client/sigil.svg` is original vector artwork authored for this project: concentric gold rings, star and geometric seal. No external source or attribution requirement. `Shaders/GildedReveal.gdshader` is original project source implementing diagonal, blinds and diamond reveals; no downloaded shader or third-party dependency. Existing generated illustrations and licensed icons/fonts are unchanged.
