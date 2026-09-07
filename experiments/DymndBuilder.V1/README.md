# Builder V1 Prototype

A standalone proof of FA wall construction from center-to-center guides. It reads the existing Dymnd asset index and wall-ribbon catalogs; no artwork is bundled or copied.

Choose a wall with the Group, SubGroup, Material, Style, Theme, Family, and Variant filters. The floor filter bar is deliberately disabled as a visible extension point. Draw lines, rectangles, or circles on a canvas sized in grid squares. Guides support 1-square, 1/2-square, 1/4-square, or no snapping, plus optional 45-degree line locking.

Build Walls paints the matching full-resolution ribbon under available end, corner, diagonal, T-junction, and cross-junction prefabs. Ribbon opacity feathers beneath placed prefabs by the configured seam-fade distance. Curves are currently approximated by short straight ribbon segments. Advanced fields allow a manual `.sut`, ribbon PNG, or corner PNG fallback when a catalog association is incomplete.

FA artwork remains at its native 200-pixel grid scale. Canvas dimensions are entered in squares, and exported PNGs are transparent at full resolution. The checkerboard, grid, and guide strokes are preview-only. Use the mouse wheel or zoom buttons to zoom around the pointer, Fit Canvas for a one-time fit, or Auto fit to keep the complete canvas visible when the window or canvas changes. Right-click a generated prefab to move it forward, backward, to the front, or to the back of the complete ribbon/prefab layer stack. Hold and drag the right mouse button to pan instead. Preview zoom never changes export resolution.

This remains separate from Dym&D Asset Companion while the product name and broader builder workflow are undecided.
