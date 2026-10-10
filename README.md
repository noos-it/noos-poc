# Tool adoption

How a request to adopt a new tool becomes a decision, and where the decisions live.

```
decisions/     one record per decision, named <date>-<tool>.md
```

Every adoption decision gets a record, including the ones nobody argued about. A record exists so
that somebody in two years, looking at a tool nobody remembers choosing, can find out why — and can
tell the difference between a thing that was examined and a thing that merely went through.

## Submitting a request

Write it as prose. There is no form to fill in: the process reads what you wrote and works out which
fields you answered, so a request is a paragraph in a ticket, not an exercise in filling boxes.

What it needs to be reviewable at all is three things:

| | |
|---|---|
| **The tool** | A named product. "Better dashboards" is not a tool. |
| **The problem** | What is painful **today**, without it. Not what the tool does. |
| **The reasoning** | Why this one rather than another, or rather than fixing it another way. |

The reasoning is the one most requests miss. "We need X because our builds are slow" states a
problem and no reasoning — the word *because* makes it look like a case has been made. A case is
something that could be checked: *"their caching is the only one that works with our monorepo
layout, and we measured it on a branch."*

Say what else you looked at and why you set it aside. Say what it costs, including the term and who
pays. Say **what data it would touch** — this is the field most often left out and the one most
likely to stop an adoption later.

If you leave something out, nothing is guessed on your behalf. It comes back to you as a list of
exactly what to add.

## What happens to it

Every request is triaged, and the triage decides how much review it gets. The route is produced by a
written rule from a calibrated model's scores, not by anybody's reading of the day:

| Route | When | What follows |
|---|---|---|
| **send back** | The request does not say enough to be reviewed | A list of what to add. Nothing is judged. |
| **fast track** | Complete, and not risky enough to spend a review on | Straight to sign-off |
| **full review** | Complete, and deserves scrutiny | A review against what we already run, then a decision |

Two runs of triage on the same request route it the same way. That is the point of the rule: the
queue stops depending on who read it. Where triage's own numbers come back soft, the request is
flagged **low confidence** — the route still follows the rule, but a person is told to look.

**Every route ends with a person.** A fast-tracked adoption is still an adoption, and one with
nobody's name on it is not governance. What changes between routes is how much the decider has been
handed, not whether somebody decides.

### The review

Where it happens, the review checks the proposal against the tools already in use, works through a
fixed rubric, and reaches one of five verdicts — including `insufficient-information`, which is a
real answer and saves a meeting.

It reviews **the request**, not just the tool. A finding is about what the proposal does or does not
establish, named as a fault in the document rather than in the person who wrote it.

**Overlap is not a verdict.** The review names tools already in the inventory that do part of the
job, because a reader deciding should see them. Adopting something that overlaps is a normal
outcome; adopting it without anyone noticing the overlap is not.

## Deciding

Approve, reject, or edit. Whatever you write in the comment becomes the record, so **conditions
belong in the comment** — if the thing has to happen before rollout, write it there and it is
preserved as an obligation.

A rejection that asks for a resubmission is recorded as *sent back*, not *declined*. The difference
matters to whoever picks it up later.

## The records

Each record carries the decision, the reasoning as it stood, and — the part that is easy to skip and
most worth having — **what was not known at the time**. An adoption that went ahead without anyone
knowing what data the tool would touch is the single most useful thing a record can preserve, and it
is the thing nobody writes down voluntarily.

Each record also states the one line it changes in the tool inventory: add, replace, or nothing.

**`supersedes` is narrow.** A tool is only recorded as replaced when the decider actually agreed to
retire it. An overlap that a decision went ahead over is recorded as *accepted*, not superseded —
quietly retiring something nobody agreed to retire would leave the next review reasoning from an
inventory that is wrong, which is worse than one that is stale. Stale is visible from the dates.
Wrong is not.

### Applying the inventory change

The records here are the decision. They are **not** applied to the inventory automatically: an
inventory line changes when a person changes it, in `noos-personas`, where
`product-selection-review` reads it from. Until then every record says so in its own last line.

## Where the process is implemented

The pipeline runs on the Noos orchestrator as the `AdoptionIntake` workflow, over four personas —
`request-triage`, `request-extract`, `product-selection-review` and `decision-record`. Those live in
`noos-personas` and `noos-workflow`. This repository holds the process and its output, not its
implementation.
