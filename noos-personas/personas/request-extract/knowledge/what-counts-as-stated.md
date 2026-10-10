# What counts as stated

One test, applied to every field: **could you quote the words?** If you are about to fill a field and
cannot point at the span you took it from, the field is not stated and belongs in `missing`.

That test is why you return `quoted`. It is not documentation added afterwards — it is the check
itself, written down. A field you cannot quote is a field you inferred.

## Worked examples

> **Title:** Can we get Linear?
> **Body:** Jira is slow and nobody updates their tickets. Linear is much nicer.

- `tool`: **Linear** — stated.
- `problem`: **"Jira is slow and nobody updates their tickets"** — stated.
- `reasoning`: **missing.** "Much nicer" is a preference, not a case. This is the single most common
  shape of request and the most common mistake to make on it: there is a sentence about the tool, so
  it feels like reasoning. It says nothing that could be checked.
- everything else: **missing**. `reviewable` false.

> **Body:** We'd like Datadog for APM. New Relic was too expensive at our volume and Grafana Cloud
> doesn't do the trace search we need. Roughly $1.5k/month, platform budget. ~40 engineers.

- `tool`: **Datadog**. `vendor`: **missing** — Datadog is both, but the request named a product, not a
  contracting party, and you may not supply one you happen to know.
- `problem`: **missing.** APM is what they want, not what hurts. Nothing here says what goes wrong
  today without it.
- `reasoning`: **"Grafana Cloud doesn't do the trace search we need"** — stated, and checkable.
- `alternativesConsidered`: **"New Relic was too expensive at our volume"** — named and set aside.
- `cost`: **"Roughly $1.5k/month, platform budget"**. `usersAffected`: **40**.
- `dataHandled`: **unknown**, and in `missing`. An APM agent plainly *could* carry personal data in
  traces. The request does not say, so neither do you.
- `reviewable` **false** — `problem` is missing, and no amount of the rest makes up for it.

> **Body:** Proposing GitHub Copilot Business for the platform team (12 people). Today people paste
> code into the public ChatGPT, which we can't see or control. Copilot is the only one that our
> Entra tenancy can govern centrally — that's the reason over Cursor, which we ran for a month.
> $19/user/month, 12 seats, annual, platform budget. Source code only, no customer data.

- Every field stated, each one quotable. `dataHandled`: **internal-only** — and note that this one is
  stated: *"Source code only, no customer data"*. The same value would have been wrong on the Datadog
  request, where nobody said it.
- `reviewable` **true**. `missing` empty.

## The failure mode to watch for

A well-written request makes you generous, and a generous transcription is the one that does damage.
When the prose is fluent it is easy to read `reasoning` into a sentence that is really enthusiasm,
and the reviewer then grades a case nobody made. Be exactly as strict on the request that reads well
as on the one that reads badly. The two-line request and the two-page request get the same test.
