# The fields, and what each one is for

These are the input contract of `product-selection-review`. They are not a form somebody designed to
be filled in; they are what a reviewer needs in order to reach a verdict. Knowing what the reviewer
does with a field tells you when it is really answered.

| Field | What the reviewer does with it | Answered when |
|---|---|---|
| `tool` | Looks it up in the inventory for overlap | A product is named |
| `vendor` | Who the organisation would be in a contract with | A company is named |
| `problem` | Judges whether the pain is real and worth spending on | Something painful *today* is described |
| `reasoning` | Judges whether the case is evidence or preference | The case for **this** tool is made |
| `alternativesConsidered` | Checks the choice was a choice | At least one other option is named |
| `cost` | Weighs it against the problem | Any figure, term or payer is given |
| `dataHandled` | Decides whether this needs a privacy review at all | The request discusses what data goes in |
| `usersAffected` | Scales everything above | A number is given |

## The three that gate the review

`tool`, `problem` and `reasoning` are required by the reviewer's contract. Without all three,
`reviewable` is false and the request goes back instead of forward. This is not a technicality: a
review of a request that names no tool, or describes no problem, produces a page of questions and no
verdict, and wastes the reviewer's run as well as the requester's week.

## `problem` and `reasoning` are different fields

This is the distinction people most often collapse, so be deliberate about it.

- **`problem`** is the state of the world without the tool. "Builds take forty minutes and people
  stop watching them."
- **`reasoning`** is why *this* tool is the answer rather than another one, or rather than fixing it
  another way. "Their caching is the only one that works with our monorepo layout; we measured it on
  a branch."

"We need X because our builds are slow" states a problem and no reasoning. The word *because* makes
it look like a case has been made, and it has not. Put it in `problem`, leave `reasoning` missing,
and the review will ask the one question worth asking.

## `dataHandled`, and why it never gets inferred

The enum is `none`, `internal-only`, `customer-personal`, `special-category`, `unknown`.

It is tempting to reason "it is an analytics tool, so `customer-personal`". Do not. What a product
is *capable* of processing is not what this organisation would put in it, and the gap between those
two is exactly what the review is supposed to surface. An inferred value closes that question before
anyone has answered it, and the answer it closes it with is a guess wearing the clothes of a fact.

`unknown` is in the enum because it is the common, correct answer. It is also the one value that
means "ask the requester", which is only true if you never reach it by reasoning.
