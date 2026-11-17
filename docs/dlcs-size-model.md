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

A PATCH sent for another reason (a changed string1, a reorder) keeps the image's existing roles, so it doesn't send
`"roles": []` and remove `unobtainable` that way either.

`Legacy` on 1.15 is still only safe for images that should stay as they are. When an image should become fully open
(a capped image after the licence-name fix, or a restricted image whose METS now allows high-res), the first sync
re-registers it with `maxUnauthorised: -1` and `roles: []`. 1.15 keeps the existing role in that case and sets
`openFullMax` to 0, so **nobody can see the image** until a second sync PATCHes the role away. So don't let syncs
run under `Legacy` on 1.15: switch to `Explicit` first (see Order of use).

## Checks

- Under `Explicit`, a sync refuses to run if the DLCS's images have no `openFullMax`, which means the DLCS is older
  than 1.13.1. The dashboard says so and the job fails with that message. A manifestation with no images on the
  DLCS yet can't be checked, so only switch to `Explicit` after the DLCS upgrade.
- The dashboard's asset page shows `maxWidth` and `openFullMax` next to `maxUnauthorised`.

## Order of use

1. Before the DLCS upgrade, every environment runs `Legacy`.
2. Pause processing for the upgrade. As soon as the DLCS reports 1.15, switch every environment to `Explicit`
   before processing resumes, so no sync runs under `Legacy` on 1.15.
3. Re-sync the affected items in batches with workflow jobs (`--populate-file`). Workflow jobs rebuild the
   manifest, so thumbnail sizes stay right; under a 1000px `maxWidth` the DLCS no longer makes a 1024px thumbnail.

`HoldAccessChanges` is a safety switch for an unplanned period under `Legacy` on 1.15: with it on, syncs list role
and size changes as mismatches but don't send them.

After the re-syncs, going back to `Legacy` doesn't change images on a sync (it compares only `maxUnauthorised`,
which `Explicit` leaves alone), but any re-registration under `Legacy` (Force Reingest, a new version from Goobi)
caps an option B image again. Fix forward instead.
