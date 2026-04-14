---
name: changelog
description: 'Alias for changelog-parse-format. Gather changelog deltas from upstream/main (Monolith-Station) and return one copyable Type: Message list.'
---

Use the changelog-parse-format workflow.

Default behavior:
1. Use `upstream/main` (Monolith-Station/Monolith) as baseline.
2. Gather missing tail changes for `Resources/Changelog/Monolith.yml` relative to local.
3. Parse each change as `Type: Message`.
4. Return exactly one fenced text code block.

If the user specifies a different changelog file, range, or baseline, follow the user override.
