# Tool adoption

How a request to adopt a new tool becomes a decision, and where the decisions live.

```
decisions/     one record per decision, named <identifier>.md
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

## Every decision has an identifier

```
20261010-linear-7b248dd7b188
└─ decided  └─ tool  └─ the run that produced it
```

The last twelve characters are the start of the workflow instance id, so a record, its file name and
the branch of the pull request that adds it all carry the same identifier, and all three trace back
to the run — its step log, and every persona call behind it. The identifier is computed by the
orchestrator, not written by a model: a generated value would be unique and would correlate to
nothing, which is the one property it exists to have.

Because it is derived rather than generated, the same decision always names the same branch. Publish
it twice and the existing pull request is updated; it does not open a second one.

Records from before this existed keep their old names. They are read by the same lookup, which falls
back to the heading when there is no front matter, so nothing had to be renamed.

## Deciding

Approve, reject, or edit. Whatever you write in the comment becomes the record, so **conditions
belong in the comment** — if the thing has to happen before rollout, write it there and it is
preserved as an obligation.

A rejection that asks for a resubmission is recorded as *sent back*, not *declined*. The difference
matters to whoever picks it up later.

## When a tool has been through here before

Before anything is reviewed, the process reads the records in this repository and looks for the tool
being proposed — and for other tools in the same category, which is where an overlap the inventory
has not caught up with will show.

If it finds one, the decider is told **first**, above the request and above any review. The case this
guards against is a tool declined last year coming back on the fast track, where by definition nobody
is spending a review on noticing.

**A repeat is a fact about the request, not a verdict on it.** Asking again is how the process is
supposed to work:

| Previously | What the question actually is |
|---|---|
| **Declined** | Has the blocker been answered? Often it has — that is why people are allowed to ask twice. |
| **Sent back** | Is what was missing now there? This is a send-back working. |
| **Adopted** | Usually more seats or another team. Occasionally nobody knew it was already running, and the inventory is stale. |

What a record said then is not what is true now. A record naming a missing DPA a year ago does not
mean there is still no DPA — it is a question to put to the proposal, never a fact about the tool.

**"No history" and "nobody could check" are different answers**, and the process keeps them apart. If
this repository cannot be read, the decider is told that in those words and the record says so under
*What was not known*. Silence is never allowed to read as a clean record.

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

### How a record gets here

As a pull request, one record per branch, named `decision/<identifier>`. The orchestrator opens it
directly — creating the branch, committing the one file and opening the pull request. No model is
involved in that step: it is four API calls in a fixed order over a record that is already final, and
a record is text somebody outside the process wrote, so nothing reads it while holding a credential
that can change a repository.

Nothing is merged automatically. Whoever reviews the pull request decides when it lands, and updating
the index table in `decisions/README.md` is part of that review rather than something a machine does
around you.

The decision is made and recorded before any of that. If this repository cannot be written to, the
record still exists as an artifact of the run and the publishing step is what failed — a delivery
problem, visible in the step log, not a lost decision.

### Applying the inventory change

The records here are the decision. They are **not** applied to the inventory automatically: an
inventory line changes when a person changes it, in `noos-personas`, where
`product-selection-review` reads it from. Until then every record says so in its own last line.

## Where the process is implemented

The pipeline runs on the Noos orchestrator as the `AdoptionIntake` workflow
([noos-workflow](https://github.com/noos-it/noos-workflow), `src/Noos.Orchestrator/Workflows/AdoptionIntake.cs`).
The personas it calls live in [noos-personas](https://github.com/noos-it/noos-personas). This repository
holds the process and its output, not its implementation.

| Step | Done by | Runs |
|---|---|---|
| Triage | Jev, called directly by the `TriageRequest` activity: one HTTP call, no language model. The route comes from its scores through a written rule (`TriageRule`). | every request |
| The review form | `request-extract` persona: fills `tool`, `problem`, `reasoning` and the rest from the prose, and lists what is `missing` | every route, including send-back |
| History | `decision-history` persona: reads the records in this repository for the same tool and the same category | every route; a repository it cannot read is reported as `reachable: false`, never as "no history" |
| Review | `product-selection-review` persona | only when the route is `full-review` **and** the form is reviewable |
| Decision | a person, through a `HumanReview` task (the noos-clients inbox or the orchestrator API) | every route |
| Record | `decision-record` persona: the Markdown record and its inventory line | every route |
| Publish | `PublishDecision` activity: branch, commit, pull request against this repository | every route; skipped and reported as not published when no token is configured |

`request-triage` still exists in the registry as a persona, but this workflow no longer calls it.

The orchestrator reads and writes this repository through one pair of settings, so the reader and the
writer cannot point at different places: `Orchestrator__Decisions__Repository` (default
`noos-it/noos-poc`), `Orchestrator__Decisions__Path` (default `decisions`), `Orchestrator__Decisions__Ref`,
and `Orchestrator__Decisions__Token` for publishing (a fine-grained token with contents and pull-request
write on this repository alone; empty means decisions are recorded but not published).
