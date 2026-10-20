# Build a Parley panel

The user ran /create-parley-panel in Parley. Build a panel that matches this request:

$ARGUMENTS

## What a panel is

A JSON spec that Parley renders into an editor window. No C# is generated, nothing is added to the
project, nothing recompiles, and only the items listed below exist.

${PANEL_SPEC}

## Workflow

1. Resolve the targets. Decide what the panel edits: assets (`Assets/...` path or GUID), scene objects
   (`Scene:Root/Child`), or a whole family of assets, which belongs in a `table` with a
   `query: 't:TypeName'`. When the request says "this" or "the selection", or carries no description at
   all, use the `selection` tool; `hierarchy` and `project_info` help you locate things. If you still
   cannot tell what the panel should edit, ask one short question and stop instead of guessing.
2. Find real property paths. Call `inspect` on every target before you write a `field`, and never
   invent a propertyPath. Use the serialized names exactly as `inspect` reports them: backing fields
   usually keep their leading underscore, nested values use dots, array elements look like
   `items.Array.data[0]`. For a `table`, inspect one representative asset.
3. Build the spec. Title it after the task. Give every target an alias in `targets` and reference the
   alias. Prefer explicit `field` items over an `inspector` dump, group with `group` and `row`, add
   `header` and `help` only where they earn their place, keep nesting at or below 6 and keep the whole
   panel readable on one screen. Add `button` items only for actions the request implies; `save_assets`
   is usually welcome after asset edits.
   For a whole family of assets of one type, always drive it from a query instead of listing aliases:
   `table` for a compact overview of a few columns, `repeat` when each asset needs real fields. A
   query-driven panel keeps up by itself when assets are added, renamed or removed. Name aliases in
   `targets` only for the objects the request names explicitly.
4. Open it. Call `panel_open` with `spec` (never with `name`). It validates the targets and property
   paths: on errors nothing opens and the message names the bad path and suggests near misses, so fix
   the spec and call again. After two failed attempts, stop and report what could not be resolved
   instead of looping.
5. Report and stop. One short message: the window title, what it edits, and that the panel is not saved
   yet, because the user names it and presses Save in the panel toolbar.

## Rules

- Do not call `panel_save`. Saving is the user's choice, from the Save button in the panel.
- Do not create, write or edit any file, asset or script: a panel is not code.
- Do not change property values. The panel exists so the user can tune them.
- Warnings are fine, for example a scene object that is not currently loaded: the panel still opens with
  a placeholder. Mention them.
- Panels heal themselves: Parley records a stable id per target and repairs the path after a rename or a
  move, and query items re-run on every project change. Do not build a second panel because an asset was
  renamed or added.
- Tool names may be host-prefixed: `panel_open` can appear as `unity_panel_open` or
  `mcp__unity__panel_open`, and `inspect` as `unity_inspect` or `mcp__unity__inspect`.
