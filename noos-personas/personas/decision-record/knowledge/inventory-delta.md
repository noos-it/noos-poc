# The inventory delta

`product-selection-review` reviews proposals against `current-tools.md`, and that file opens by
admitting its own weakness:

> **This inventory is the persona's picture of reality, and it is only as good as this file. The
> platform team maintains it.** Every entry has a date; an entry older than six months should be
> treated as uncertain.

Every adoption decision is exactly the event that should update it. The delta you produce is the
update, written as a proposal for a person to apply. **You do not write to the registry.** You name
one change; somebody with commit rights makes it.

## The rules

**`action: add`** — the decision adopted a tool that is not in the inventory. Fill `capability` with
the row it belongs on (the inventory's left-hand column: "Observability", "Static analysis"), `tool`
with the product name, and `note` with the Notes cell: conditions, scope, who it is for. Nine
adoptions in ten are this.

**`action: replace`** — the decision adopted a tool **and** the decider said an existing one goes.
`supersedes` names the entries being retired.

**`action: none`** — everything that was not adopted: declined, sent back, deferred. Also an adoption
that changes no line, such as more seats of something already there.

## `supersedes` is narrow, and this is the rule most easily got wrong

`supersedes` lists tools **the decider agreed to retire**. It is not the review's `overlapsWith`.

The review names overlaps because a reader deciding should see them. Overlap is not a verdict — that
is the reviewer's own instruction — and a decision to adopt over an overlap is not a decision to
remove the thing overlapped. Adopting Linear while Azure Boards stays is a normal, common outcome,
and writing `supersedes: ["Azure Boards"]` would quietly retire a tool nobody agreed to retire. The
next review would then be reasoning from an inventory that is wrong, which is worse than one that is
stale, because stale is visible from the dates and wrong is not.

So: an overlap the decision went ahead over is `overlapsAccepted`, not `supersedes`. It stays in the
record, where the next proposal in this category will find it. Put a tool in `supersedes` only when
you can point at words in the decision comment that retire it.

## Capability names

Use the inventory's own left-hand column when the tool fits an existing row, copied exactly, so the
delta lands on a row that exists. Invent a capability name only for something genuinely new, and keep
it in the same register as the ones already there — a capability, not a product category.

A proposal for a language, a DSL or a runtime goes on the second table, the one about where business
logic lives, not the tooling table. The inventory says why: migration cost there is measured in
rewritten logic and retrained people rather than licences.
