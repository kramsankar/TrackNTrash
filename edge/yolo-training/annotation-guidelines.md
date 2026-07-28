# Carton Annotation Guidelines

Single class: **`carton`**. Goal: an accurate **count** of cartons on a tray from a top-down view.

## What to label

- **One bounding box per visible carton top face**, including partially occluded ones — if a human can tell a distinct carton is there, box it.
- Box the **top face** tightly (the surface the camera sees), not the full 3-D extent.
- Label cartons at the frame edge if **> ~50%** of the top face is visible; otherwise skip (it belongs to a neighbouring tray/zone).

## What NOT to label

- The tray/crate itself.
- Cartons clearly outside the staging zone (neighbouring trays).
- Straps, tape rolls, hands, paperwork.
- Reflections/shadows.

## Occlusion & stacking

- **Overlapping cartons**: draw a separate box for each, even where edges are hidden — estimate the occluded top-face extent.
- **Stacked (2 layers)**: label only tops the camera actually sees. The dock counts what is visible top-down; the ASN expected count is set accordingly by ops. Document any site that ships multi-layer trays so the manifest count matches the visible-top convention.

## Consistency rules

- One annotator style guide; spot-audit 5% for inter-annotator agreement (target IoU agreement > 0.9).
- Ambiguous frames → flag for review rather than guess.
- Keep filenames stable; labels mirror image paths (`images/... ↔ labels/...`).

## Coverage targets (1500 images)

| Condition | Min share |
|-----------|-----------|
| Bright / even light | 25% |
| Dim / uneven | 20% |
| Glare / specular | 15% |
| Partial occlusion / leaning | 20% |
| Empty tray (0 cartons) | 5% |
| Over-count (extra carton present) | 10% |
| Mixed carton sizes/brands | 5% |

Empty-tray and over-count images are essential so the counter is correct at the boundaries that drive `MISSING_CARTON` / `UNKNOWN_CARTON` verdicts.
