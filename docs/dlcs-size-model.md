# How the DDS limits image sizes on the DLCS

Protagonist 1.13.1 replaced an image's `maxUnauthorised` with two fields (see the DLCS's
[ADR 0010](https://github.com/dlcs/protagonist/blob/develop/docs/adr/0010-replace-maxunauthorised.md)):

- `maxWidth`: the largest response anyone may get, logged in or not. 0 means the system default.
- `openFullMax`: when the image has roles, the largest `/full/` response available without one.

Its migration rewrote existing images to keep their behaviour. An image with no roles and `maxUnauthorised` of
0 or more got the role `https://dlcs.io/roles/unobtainable`, which nobody has, with `openFullMax` set to the old
`maxUnauthorised`. The API still accepts `maxUnauthorised` and converts it the same way.

Three settings in the `Dlcs` section choose how the DDS works with this. They're read by the dashboard, the
workflow processor and the job processor. As environment variables they're `Dlcs__SizeModel`,
`Dlcs__RestrictedDownloadMaxWidth` and `Dlcs__HoldAccessChanges`.

| Setting | Default | What it does |
| --- | --- | --- |
| `SizeModel` | `Legacy` | `Legacy` sends `maxUnauthorised`, as the DDS always has. `Explicit` sends roles, `maxWidth` and `openFullMax`, never `maxUnauthorised`, and needs Protagonist 1.13.1 or later. |
| `RestrictedDownloadMaxWidth` | `1000` | `Explicit` only: the `maxWidth` for open images whose METS doesn't permit `wholeImageHighResAsJpg`. `1000` lets anyone zoom in, with no single response over 1000px; `0` leaves them unlimited. |
| `HoldAccessChanges` | `false` | If `true`, a sync still works out role and size changes for images already on the DLCS, and lists them on the dashboard as "HELD" mismatches, but doesn't send them. New images are registered as normal. |

## What each kind of image gets

| Image | `Legacy` | `Explicit` | Protagonist 1.13.1+ stores, from `Legacy` |
| --- | --- | --- | --- |
| Open, high-res permitted | `maxUnauthorised: -1` | `maxWidth: 0`, `openFullMax: 0` | no role, `openFullMax: 0` |
| Open, high-res not permitted | `maxUnauthorised: 1000` | no role, `maxWidth: 1000`, `openFullMax: 0` | **`unobtainable`**, `openFullMax: 1000` |
| Clickthrough | its role, `maxUnauthorised: 200` | its role, `openFullMax: 200` | its role, `openFullMax: 200` |
| Other restricted | its role, `maxUnauthorised: 0` | its role, `openFullMax: 0` | its role, `openFullMax: 0` |
| AV and files | `maxUnauthorised: -1` | `maxWidth: 0`, `openFullMax: 0` | as registered, `openFullMax: 0` |

Only the second row differs between `Explicit` and what the migration made of `Legacy`. So switching to
`Explicit` re-ingests exactly those images, once: they lose the `unobtainable` role and gain `maxWidth`. That
lets anyone deep zoom, with each response at most 1000px. The `unobtainable` role allowed `/full/` requests up to
1000px and refused tiles to everyone.

Changes to `maxUnauthorised`, `maxWidth` or `openFullMax` are re-ingests: the DLCS doesn't allow them in a batch
PATCH, and they move thumbnails between open and auth storage. Role changes are batch PATCHes, which Protagonist
1.14+ turns into a re-ingest when an image gains or loses its only role.

## Legacy on Protagonist 1.13.1 and later

Under `Legacy`, an image the DDS expects to have no roles and `maxUnauthorised` of 0 or more matches the
`unobtainable` role on the DLCS. Without this the sync would PATCH the role away, Protagonist 1.14+ would
re-ingest the image, and it would come back with no size limit at all. On Protagonist 1.12 no image has that role,
so `Legacy` behaves exactly as before.

If an image with the `unobtainable` role should become fully open under `Legacy` on 1.15 (for example after a METS
change), the first sync re-registers it with `maxUnauthorised: -1`. The DLCS keeps its roles in that case, so a
second sync removes the role.

## Checks

- Under `Explicit`, a sync refuses to run if the DLCS's images have no `openFullMax`, which means the DLCS is older
  than 1.13.1. The dashboard says so and the job fails with that message. A manifestation with no images on the
  DLCS yet can't be checked, so only switch to `Explicit` after the DLCS upgrade.
- The dashboard's asset page shows `maxWidth` and `openFullMax` next to `maxUnauthorised`.

## Order of use

1. Before the DLCS upgrade, every environment runs `Legacy`.
2. For the upgrade and the days after, set `HoldAccessChanges` to `true`.
3. Afterwards, one environment at a time: `HoldAccessChanges` back to `false`, then `SizeModel` to `Explicit`, then
   re-sync the affected items in batches (`--populate-file`).

Going back from `Explicit` to `Legacy` is safe until those re-syncs. After them, `Legacy` would cap the images
again, so fix forward instead.
