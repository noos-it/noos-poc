# The record

Markdown, written to `decision.md`. An ADR, kept short. Nobody reads a long one, and the parts that
matter are the context and the consequences rather than the prose.

Omit a section that has no content rather than writing "none" — except **What was not known**, which
is written even when empty, because an empty one is itself a claim.

```markdown
# <Tool> — <adopted | adopted with conditions | declined | sent back | deferred>

| | |
|---|---|
| **Decided** | <date> by <decider, or "not recorded"> |
| **Requested** | by <requester, or "not recorded"> |
| **Category** | <category> |
| **Cost** | <cost as given, or "not stated"> |
| **Data it touches** | <dataHandled> |
| **People** | <usersAffected, or "not stated"> |

## Decision

<One paragraph: what was decided, in the decider's terms. Quote the comment where it is decisive.>

## Why

<The case as it stood. Where a review ran: its verdict, its summary, the counts. Where none ran: the
route triage gave it and why that was enough — fast-track means complete enough and not risky enough
to spend a review on, and saying so is the justification.>

## Conditions

<One bullet per condition, each an obligation. Name the owner only if the comment named one.>

## Overlaps accepted

<Tools already in the inventory that do part of this job, which this decision went ahead over. Not
retired — still running. Omit the section when the review raised no overlaps.>

## What was not known

<The `missing` fields, as they were at the time. `dataHandled: unknown` belongs here and goes first.
Where triage flagged `lowConfidence`, say that the route rests on soft numbers. Write "Nothing: the
request answered every field" when that is true — rare, and worth recording.>

## Inventory

<The delta in one line: "Add <tool> under <capability>", "Replace <old> with <tool> under
<capability>", or "No change". Then: "Not yet applied to current-tools.md.">
```

## Tone

Flat and factual. This is a record, not an argument, and the reader is a stranger. No adjectives for
the decision, no praise for the requester, no editorialising about whether it was wise. The most
useful sentence you can write is the one that says plainly what was not known at the time.
