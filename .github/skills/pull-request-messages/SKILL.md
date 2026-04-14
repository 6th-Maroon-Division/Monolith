---
name: pull-request-messages
description: 'Draft repository-ready pull request messages for 6MD-Monolith. Use when writing a PR description, pull request body, changelog note, breaking change summary, test plan, or final self-review from a code diff.'
argument-hint: 'Summarize the change, affected systems, tests run, and any balance or breaking impact'
user-invocable: true
---

# Pull Request Messages

Generate a complete pull request message that fits this repository's PR template and reflects the actual code changes.

## When to Use

- The user asks for a pull request message, PR body, changelog entry, or self-review summary.
- The task is to turn a diff, changed files, or recent implementation work into reviewer-facing text.
- The PR needs testing notes, balance impact, media guidance, or breaking change disclosure.

## Inputs To Gather

Collect the following before drafting:

1. What changed at a high level.
2. Why the change was made.
3. Whether there is an issue number that should be closed by the PR.
4. Which systems, prototypes, configs, or player-facing behaviors changed.
5. What validation was performed, including targeted tests and any known gaps.
6. Whether the change affects game balance, admin workflows, content behavior, or public APIs.
7. Whether media is needed because the change is visible in game.
8. Whether a changelog entry is appropriate.

If the user did not provide enough detail, inspect the diff, changed files, relevant tests, and repository template before writing.

## Required Repository Context

Read and follow [the PR template](../../PULL_REQUEST_TEMPLATE.md).

The generated message must use this exact section order and heading text:

1. About the PR
2. Closes issue
3. Why / Balance
4. Media
5. Requirements
6. How to test
7. Breaking changes
8. Changelog

## Procedure

1. Inspect the current change set.
2. Identify the reviewer-relevant behavior changes rather than restating file edits.
3. Map the changes onto the repository template sections.
4. Write concise, concrete prose for each section using the exact heading order defined in this skill.
5. If an issue number is provided, insert a `Closes #1234` line between `About the PR` and `Why / Balance`.
6. Only claim tests, media, or requirements that are supported by the available evidence.
7. If the change is internal-only, explicitly state that media, balance notes, or changelog are not needed.
8. If the change is player-facing, include a changelog block unless there is a strong reason not to.

## Writing Rules

- Prefer concrete behavior summaries over implementation trivia.
- Mention game balance implications when mechanics, salvage, economy, access, combat, or progression are affected.
- Include exact testing steps when automated coverage is incomplete.
- Call out breaking changes explicitly, including renamed prototypes, changed public methods, namespace moves, config changes, or migration needs.
- Do not fabricate issue links, media, tests, or approvals.
- When an issue number is provided, use a standalone `Closes #1234` line after `About the PR`.
- Preserve uncertainty: if something is not verified, say so.
- Wrap the final PR message in a single fenced `md` code block so it can be copied in one action.
- Do not add any headings, sections, or explanatory prose outside the locked format.
- Do not rename, reorder, or omit headings except for the conditional `Closes #1234` line.

## Output Format

Produce a repository-ready PR body inside one fenced `md` code block using exactly this structure:

````md
```md
## About the PR
...

Closes #1234

## Why / Balance
...

## Media
...

## Requirements
- [ ] I have read relevant guidelines/documentation to this PR found on our devwiki.
- [ ] I have added media to this PR or it does not require an ingame showcase.
- [ ] I can confirm this PR contains either no AI-generated content, or AI-generated content that meets our guidelines.

## How to test
...

## Breaking changes
...

## Changelog
:cl:
- tweak: Added a server toggle to force breathable atmosphere on salvage expedition planets (enabled by default).
```
````

When no issue number is given, omit the `Closes #1234` line entirely.

When a changelog is not needed, say so directly instead of forcing an empty entry.

## Locked Format Rules

- The output must begin with `## About the PR`.
- The next content after the About section is either `Closes #1234` or directly `## Why / Balance` when no issue number was provided.
- `## Media`, `## Requirements`, `## How to test`, `## Breaking changes`, and `## Changelog` must appear in that order.
- The `## Changelog` heading must be used instead of bold text.
- The `:cl:` block belongs directly under `## Changelog`.
- The entire response should be the copyable `md` code block unless the user explicitly asks for commentary too.

## Completion Checks

The draft is complete when:

- Every template section is either filled or explicitly marked not applicable.
- The summary explains reviewer-visible behavior, not just touched files.
- The issue-closing line is present only when the user supplied an issue number.
- The headings and section order match the locked format exactly.
- Test coverage and manual verification steps are stated accurately.
- Balance impact is addressed when relevant.
- Breaking changes are either documented or explicitly ruled out.
- The entire PR body is wrapped in one copyable fenced block.
- No unsupported claims were added.

## Follow-up Questions To Ask When Needed

Ask targeted follow-ups only for missing facts that materially affect the draft, such as:

- Is there an issue number this PR should close?
- Is this change player-facing enough to require media?
- Should this include a changelog entry?
- Were there manual test steps beyond the automated tests?
- Does this alter balance, progression, or admin workflows?
- Are any prototype names, public APIs, or config keys changing?