# Demo asset sources

Kenney, **UI Pack - Adventure 1.1** (archive version; created 2024-08-13).

- Pack: https://kenney.nl/assets/ui-pack-adventure
- Download: https://kenney.nl/media/pages/assets/ui-pack-adventure/9a877376bc-1723597274/kenney_ui-pack-adventure.zip
- License: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/
- Included original license: `Adventure/License.txt`.
- Included files: parchment panel, brown/grey buttons, curtain banner, checkbox textures, scrollbar, green progress strip, and round slider handle.
- PNG textures retain the original artwork. `slider_handle.svg` derives from `Vector/round_brown.svg`: only viewport size changed to 28 × 28 with the original 64 × 64 viewBox, to fit the slider control.
- Retrieved 2026-10-02. Attribution is optional; this Demo credits Kenney.

The Nunito Project Authors, **Nunito** variable font.

- Project: https://github.com/googlefonts/nunito
- Distribution: https://github.com/google/fonts/tree/main/ofl/nunito
- Download: https://raw.githubusercontent.com/google/fonts/main/ofl/nunito/Nunito%5Bwght%5D.ttf
- License: SIL Open Font License 1.1; original license and copyright notice included in `Fonts/OFL.txt`.
- Included unmodified font: `Fonts/Nunito.ttf`. The Theme configures its weight and rendering; it does not modify the font file.
- Retrieved 2026-10-02.

`Themes/Adventure.tres` defines the Demo palette, texture slicing, typography, focus ring and control states. It is project presentation, not a core framework style system.

All included assets are local. No runtime downloads or external image dependencies.

## Item portraits / Partner Screen Demo

Icons made by **Lorc** and **Delapouite**, from [game-icons.net](https://game-icons.net), licensed under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/).

- Official repository: https://github.com/game-icons/icons ; contributor/license notice retained in `Items/License.txt`.
- Delapouite (https://delapouite.com): `bow-arrow`, `diamond-ring`, `belt-armor`, `herbs-bundle`, `sewing-string`, `amber-mosquito`.
- Lorc (https://lorcblog.blogspot.com): `hood`, `boots`, `compass`, `crystal-growth`, `rock`, `dead-wood`.
- Source SVGs: `https://raw.githubusercontent.com/game-icons/icons/master/{author}/{name}.svg`, with the author and names listed above. Retrieved 2026-10-02, bundled under `Items/`.
- Modifications: removed the black background rectangle; changed white foreground fill to the Demo's dark teal `#335653`. Original path geometry/viewBox retained. The Demo renders the same modified texture at thumbnail and portrait sizes.
- Visible attribution is included in the camp menu; this file provides the source, license links and modification notice for redistribution.
