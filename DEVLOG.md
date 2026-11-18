# Materials devlog

What landed in Materials, by day and newest first, with the pictures taken on the way, the captures
the repository keeps under `.github/assets` and the pictures made while an item was built, kept
under `.github/assets/readings` as WebP files named by their day, each on the day its name says, and
the notes written beside them as Markdown files named the same way. A line under Done names the item
of .github/REVIEW.md or .github/REVIEW-ARCHIVE.md and the commit that closed it.

## 2026-11-18

![](.github/assets/readings/2026-11-18-item-3-bc7-axis.webp)
The sample's ringed albedo as drawn, on the left, as the BC7 encoder wrote it with its power
iteration started at (1, 1, 1, 1), in the middle, each block flat at its mean and 23.70 dB over the
picture, and as it writes it started from the column of the channel that varies most, on the right,
34.30 dB, the whole picture above and its top-left block below, where Modeling's carpet holds at
45.27 dB and its wallpaper goes from 46.15 to 46.16.

![](.github/assets/readings/2026-11-18-item-3-prototype-tables.webp)
The seven prototypes of the module's one table, a meter's cell of the grid at 256 pixels tinted in
linear light, floor, wall and ceiling one near white at sRGB 0.90, stairs orange, door green, window
blue and hazard red, over a grid whose face is 0.99, its lines 0.6 and its quarters' lines 0.82.

![](.github/assets/readings/2026-11-18-item-3-split-sum-fit.webp)
Karis's fit of the split sum against the split sum integrated by importance sampling, the share of
the light from all around a surface reflects toward the eye, roughness 0 to 1 across and the cosine
to the eye from 1 at the top to 0 at the bottom, a dielectric of f0 0.04 above and a white metal of
f0 1 below, the difference red where the fit is higher and blue where it is lower, full at 0.049
above and 0.415 below. The dielectric's largest difference is 0.084, at a roughness and a cosine of
0.12, and over the hits of rays arriving at random, whose cosine has a density of twice itself, its
mean is 0.0067 with 15.8% of the hits past 0.01. The metal's largest is 0.509, at a roughness of
0.99 seen at a cosine of 0.008, its mean over the hits 0.10 with 95.6% past 0.01, the fit holding
0.45 at a roughness of one where the integral worked by hand gives 0.3069 head on.

![](.github/assets/readings/2026-11-18-item-3-bc7-modes.webp)
The BC7 encoder by content, each picture 64 by 64 texels at four pixels a texel, its source on the
left, mode 6 alone in the middle and modes 6 and 1 on the right, mode 1 tried for an opaque block
whose mode 6 error passes a mean of 4 a channel, 42 dB; a two-color edge holds at 49.38 dB both
ways, three flat colors a block go from 16.04 to 54.15 dB, two gradients a block from 24.12 to
44.49, lettering on a varied ground from 30.96 to 37.78 and a corner of Modeling's scanned wallpaper
from 46.39 to 50.06, at about 3 ms a block tried against 0.12 for mode 6 alone, Modeling's carpet
taking 0.93 ms a block for 46.40 dB where it took 0.12 for 45.27.
