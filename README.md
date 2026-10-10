# AdoptionIntake

A workflow for the Noos orchestrator, and the two personas it needed, for getting a tool adoption
request from prose to a decision somebody can find again.

The files here belong in two other repositories. They are staged in this one so the change can be
read in one place before it is split. **Nothing here is wired into a running system by merging it**;
see *Applying it* below.

```
noos-personas/personas/request-extract/     new persona
noos-personas/personas/decision-record/     new persona
orchestrator/src/.../Workflows/AdoptionIntake.cs   the workflow
orchestrator/src/.../Workflows/WorkflowJson.cs     reading persona answers inside a workflow
orchestrator/tests/.../AdoptionIntakeTests.cs      10 tests
orchestrator/wiring.diff                    the three existing files that change
```

## Why

`request-triage` already ends with a routing rule that names its successor:

> otherwise `scrutiny` of **4 or more** → `full-review`. **Hand it to `product-selection-review`.**

Their `category` enums are the same twelve values in the same order, written to be passed through.
But the handoff could not happen: triage reads free text (`title`, `body`) and the reviewer requires
structure (`tool`, `problem`, `reasoning`). Nothing turned one into the other, so a person retyped
the ticket into a form, and nothing chained the two personas at all.

`request-extract` is that missing step. `decision-record` is the other end: the decision, written
down, with the one line it changes in the inventory.

## The flow

```
POST /tasks {workflow:"AdoptionIntake", input:{title, body, requestedBy?}}
   │
   ├─ request-triage ──────────────► {route, category, scrutiny, lowConfidence, note}
   │
   ├─ request-extract ─────────────► the review form + `missing` + `reviewable`
   │     (every route, including send-back: `missing` is what the requester gets told to add)
   │
   ├─ product-selection-review ────► only when route = full-review AND reviewable
   │
   ├─ HumanReview ─────────────────► every route. A fast-track adoption is still an adoption
   │
   └─ decision-record ─────────────► ADR + inventory delta, as artifacts
```

Three things are worth knowing about the design:

**The route is applied, not decided.** Triage returns a route that a written rule produced from a
calibrated model's numbers, and the workflow switches on it without re-judging. That is the whole
reason the rule lives in a persona rather than in a prompt: *"two runs of this persona on the same
request should route it the same way."*

**A route to review is not the same as something to review.** `full-review` with `reviewable: false`
goes to the person, not to the reviewer. `product-selection-review` requires `tool`, `problem` and
`reasoning`; without them it buys a page of questions at the price of the expensive model.

**The failure directions are chosen.** A triage answer that will not parse routes to the *full*
review rather than past it, and a human decision the workflow does not recognise faults the run
rather than recording an approval nobody gave.

## Verified

`dotnet test tests/Noos.Orchestrator.Tests` — 46 pass (36 existing, 10 new).

Live against the running stack, all three routes, with real models and real Jev calls:

| Request | Triage | Review | Decision | Record |
|---|---|---|---|---|
| Linear, no vendor or data stated | `send-back`, completeness 0.28, low confidence | skipped | reject, resubmit | `sent-back`, delta `none` |
| Copilot Business, fully specified | `fast-track`, completeness 0.98, scrutiny 2.8 | skipped | approve + conditions | `adopted-with-conditions`, delta `add` under *AI and ML* |
| Analytics over a claims portal, health data, US processing | `full-review`, scrutiny 4 | `revise-proposal`, 1 blocker, 4 questions, overlaps named | reject | — |

The middle row is the one to notice: a complete, unalarming request never reaches the expensive
reviewer, which is the point of putting a cheap calibrated model in front of the queue.

## Applying it

The two persona directories drop into `noos-personas/personas/`. Validate, and they are served
within seconds by a gateway with `Registry__Watch`:

```bash
cd noos-personas && dotnet run --project ../noos/src/Noos.Cli -- validate . --strict
```

The two workflow files drop into `noos-workflow/src/Noos.Orchestrator/Workflows/`, the test file into
`tests/Noos.Orchestrator.Tests/`, and `wiring.diff` carries the three existing files that change:

* `OrchestratorOptions.cs` — four role entries, so the personas are remappable like every other role
* `WorkflowCatalog.cs` — the entry that lets `POST /tasks` start it by name
* `docker-compose.yml` — the matching `PERSONA_*` overrides

```bash
cd noos-workflow && git apply ../noos-poc/orchestrator/wiring.diff
```

## Open

* **`fast-track` asks for a sign-off.** It could auto-approve and only record. The argument for
  keeping a person is that an adoption with nobody's name on it is not governance, and fast-tracked
  ones are the ones most likely to be forgotten — but it is a policy choice, not a technical one.
* **`decision-record` does not write to the registry.** It emits the inventory delta as an artifact
  for a human to apply. Giving it a credential and a push would close the loop and widen its blast
  radius from "writes a file in a sandbox" to "opens pull requests"; worth doing deliberately or not
  at all.
* **The step log holds hashes, not payloads**, so the decision comment is in the record artifact and
  the Elsa instance, not in `orchestration_step_log`. That is the existing PII posture and this
  workflow does not change it — but adoption requests carry names, and the artifacts outlive them.
