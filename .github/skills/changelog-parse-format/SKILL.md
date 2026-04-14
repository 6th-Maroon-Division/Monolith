---
name: changelog-parse-format
description: 'Gather SS14 changelog changes from upstream, then parse them into one-line records in the exact format ChangeType: ChangeMessage. Use when extracting changelog tails from upstream, converting YAML changelog blocks, normalizing mixed changelog text, or formatting missing changelog entries for quick review.'
argument-hint: 'Provide changelog source context (file, branch/range, or pasted entries) and output scope'
user-invocable: true
---

# Changelog Parse Format

Gather changelog changes from upstream first, then convert them to a flat, grep-friendly list using this exact output format:

ChangeType: ChangeMessage

## When To Use

- User asks to parse changelog entries into a compact list.
- User asks for missing changelog entries and wants line-per-change output.
- User asks to gather changes from upstream first and then format the output.
- Input is YAML changelog entries with `type` and `message` fields.
- Input is pasted changelog text that should be normalized.

## Default Workflow

1. Identify upstream baseline.
	In this repository, default baseline is `upstream/main` (Monolith-Station/Monolith).
	Only use another baseline if the user explicitly asks.
2. Gather the requested changelog delta from upstream (for example missing local tail, explicit range, or full file scope).
3. Parse gathered entries into one line per change.
4. Return final output in one copyable block.

## Required Output Rules

1. Use exactly one change per line.
2. Use exactly this delimiter: `: ` (colon + space).
3. Do not wrap values in parentheses, quotes, bullets, or markdown formatting.
4. Preserve the original change type casing when available (for example: `Add`, `Tweak`, `Fix`, `Remove`).
5. Collapse multiline messages to one line by replacing internal newlines and repeated whitespace with single spaces.
6. Keep punctuation that is part of the message text.
7. Skip metadata fields such as author, id, time, url, and section headers.
8. Return parsed lines in one copyable code block by default.

## Parsing Rules

- For YAML blocks, pair each `type` with its sibling `message`.
- If message uses folded/literal YAML (`>-` or `|-`), join continuation lines into a single sentence.
- If an entry contains multiple change objects, output one line per change object.
- If a type exists without message, skip it.
- If message exists without type, skip it unless user explicitly requests fallback types.

## Placeholder Clarification

If the user provides a format with placeholders like `(changetype): (changemessage)`, treat parentheses as illustrative placeholders unless the user explicitly requests literal parentheses in output.

## Optional Upstream Tail Workflow

When asked for changes missing from local relative to upstream:

1. Determine baseline file from `upstream/main` by default.
2. Compare local vs baseline line count (or requested range method).
3. Extract only the requested tail/range.
4. Parse extracted entries with the required one-line format.

## Invocation Preview

Use this skill when you want upstream-derived change lines in `Type: Message` format.

Examples:

- `Use changelog-parse-format to gather missing changes in Resources/Changelog/Monolith.yml from upstream/main and output Type: Message lines in one copy block.`
- `Run changelog-parse-format for Resources/Changelog/Frontier.yml full upstream file and return parsed Type: Message lines.`
- `Use changelog-parse-format on this pasted changelog YAML and normalize it to Type: Message in a single code block.`

## Output Container

- Wrap the final parsed list in exactly one fenced text code block by default.
- Do not add bullets inside the block.
- Keep each parsed change on its own line as `Type: Message`.
- If user explicitly asks for non-block output, follow that request.

## Final Check

Before returning output:

- Confirm every line matches `Type: Message`.
- Confirm no metadata-only lines remain.
- Confirm one copyable code block is used unless user requested another format.
