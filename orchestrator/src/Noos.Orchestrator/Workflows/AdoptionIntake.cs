using System.Globalization;
using System.Text;
using System.Text.Json;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Noos.Orchestrator.Activities.Activities;
using Noos.Orchestrator.Activities.Context;
using Noos.Orchestrator.Activities.Runs;
using static Noos.Orchestrator.Workflows.WorkflowHelpers;
using static Noos.Orchestrator.Workflows.WorkflowJson;

namespace Noos.Orchestrator.Workflows;

/// <summary>
/// Tool adoption, from a request written in prose to a decision somebody can find again.
/// <para>
/// <c>request-triage</c> routes it (a calibrated model's numbers, then a written rule), <c>request-extract</c> turns the prose into
/// the review form, <c>product-selection-review</c> runs only on the route that asks for it, a person decides, and
/// <c>decision-record</c> writes the decision down with the inventory line it implies.
/// </para>
/// <para>
/// Workflow input <c>Input</c>: <c>{title, body, requestedBy?}</c> as JSON. The route is the orchestrator's to apply, not a
/// persona's to decide - triage returns it already applied, and this workflow switches on it without re-judging, which is the
/// whole reason that rule lives in a persona with a calibrated model behind it.
/// </para>
/// </summary>
public class AdoptionIntake : WorkflowBase
{
    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId("noos-adoption-intake");
        builder.Name = "AdoptionIntake";
        builder.Description = "Triage an adoption request, extract the review form, review it when the route says so, let a person decide, and record the decision.";

        var task = Var<TaskContext>("task");
        var requestJson = Var<string>("requestJson");
        var triageJson = Var<string>("triageJson");
        var formJson = Var<string>("formJson");
        var reviewJson = Var<string>("reviewJson");
        var route = Var<string>("route");
        var reviewable = Var<bool>("reviewable");
        var reviewed = Var<bool>("reviewed");
        var decision = Var<string>("decision");
        var comment = Var<string>("comment");
        var decidedBy = Var<string>("decidedBy");

        builder.Root = new Sequence
        {
            Variables = { task, requestJson, triageJson, formJson, reviewJson, route, reviewable, reviewed, decision, comment, decidedBy },
            Activities =
            {
                new SetVariable<TaskContext>(task, NewContext),
                new SetVariable<string>(requestJson, ReadRequest),

                // 1. Triage. Jev answers, the persona applies the routing rule, and the route comes back already decided.
                new RunPersona
                {
                    Name = "triage", StepName = new("triage"), PersonaId = new(Persona("RequestTriage")),
                    Input = new(ctx => Pick(requestJson.Get(ctx), "title", "body", "requestedBy")),
                    Context = new(task), UpdatedContext = new(task),
                    StructuredResultJson = new(triageJson!), FailOnError = new(true),
                },
                // An unparseable triage answer routes to the full review rather than past it: the expensive path is the safe
                // default, and the cheap paths are the ones that need a reason.
                new SetVariable<string>(route, ctx => Str(triageJson.Get(ctx), "route") ?? "full-review"),

                // 2. The form. It runs on every route, including send-back: `missing` is what the requester gets told to add,
                // so the path that returns a request is exactly the one that needs the form filled in.
                new RunPersona
                {
                    Name = "extract", StepName = new("extract"), PersonaId = new(Persona("RequestExtract")),
                    Input = new(ctx => Pick(requestJson.Get(ctx), "title", "body", "requestedBy")
                        .With("category", Str(triageJson.Get(ctx), "category"))),
                    Context = new(task), UpdatedContext = new(task),
                    StructuredResultJson = new(formJson!), FailOnError = new(true),
                },
                new SetVariable<bool>(reviewable, ctx => Bool(formJson.Get(ctx), "reviewable")),

                // 3. The review, only when the route asks for it AND there is something to review. A request that names no tool,
                // no problem or no reasoning cannot be reviewed whatever its scrutiny score: product-selection-review requires
                // those three, and running it without them buys a page of questions for the price of the expensive model.
                new If(ctx => string.Equals(route.Get(ctx), "full-review", StringComparison.Ordinal) && reviewable.Get(ctx))
                {
                    Then = new Sequence
                    {
                        Activities =
                        {
                            new RunPersona
                            {
                                Name = "review", StepName = new("review"), PersonaId = new(Persona("ProductSelectionReview")),
                                Input = new(ctx => Pick(formJson.Get(ctx),
                                    "tool", "vendor", "category", "requestedBy", "problem", "reasoning",
                                    "alternativesConsidered", "cost", "dataHandled", "usersAffected")),
                                Context = new(task), UpdatedContext = new(task),
                                StructuredResultJson = new(reviewJson!), FailOnError = new(true),
                            },
                            new SetVariable<bool>(reviewed, _ => true),
                        },
                    },
                },

                // 4. The person. Every route reaches this: a fast-track adoption is still an adoption, and one with nobody's name
                // on it is not governance. What changes per route is how much the decider has been handed, not whether they decide.
                new HumanReview
                {
                    Name = "decision", StepName = new("decision"),
                    Title = new(ctx => ReviewTitle(route.Get(ctx), reviewable.Get(ctx), triageJson.Get(ctx), reviewJson.Get(ctx))),
                    Content = new(ctx => Brief(requestJson.Get(ctx), triageJson.Get(ctx), formJson.Get(ctx), reviewJson.Get(ctx))),
                    Context = new(task), UpdatedContext = new(task),
                    Decision = new(decision!), Comment = new(comment!), DecidedBy = new(decidedBy!),
                },

                // 5. The record, and the inventory line the decision implies.
                new RunPersona
                {
                    Name = "record", StepName = new("record"), PersonaId = new(Persona("DecisionRecord")),
                    Input = new(ctx => RecordInput(
                        requestJson.Get(ctx), triageJson.Get(ctx), formJson.Get(ctx),
                        reviewed.Get(ctx) ? reviewJson.Get(ctx) : null,
                        decision.Get(ctx), comment.Get(ctx), decidedBy.Get(ctx))),
                    Context = new(task), UpdatedContext = new(task), FailOnError = new(true),
                },
            },
        };
    }

    /// <summary>The request as given. A title and a body are the whole contract, and both personas that read it require them.</summary>
    private static string ReadRequest(Elsa.Expressions.Models.ExpressionExecutionContext ctx)
    {
        var raw = ctx.GetInput<string>("Input");
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("AdoptionIntake needs the input 'Input': {\"title\": ..., \"body\": ..., \"requestedBy\": ...}.");
        }

        if (string.IsNullOrWhiteSpace(Str(raw, "title")) || string.IsNullOrWhiteSpace(Str(raw, "body")))
        {
            throw new InvalidOperationException("AdoptionIntake's 'Input' needs a non-empty 'title' and 'body'.");
        }

        return raw;
    }

    private static string ReviewTitle(string? route, bool reviewable, string? triageJson, string? reviewJson) => route switch
    {
        "send-back" => $"Send back: the request is not complete enough to review{Soft(triageJson)}",
        "fast-track" => $"Sign-off: fast-tracked, no review{Soft(triageJson)}",
        _ when !reviewable => $"Decide without a review: the request names no tool, problem or reasoning{Soft(triageJson)}",
        _ => $"Decide: review says {Str(reviewJson, "verdict") ?? "nothing usable"}{Soft(triageJson)}",
    };

    /// <summary>
    /// HumanReview reports <c>Approved</c>/<c>Rejected</c>/<c>Edited</c>; decision-record's contract is
    /// <c>approve</c>/<c>reject</c>/<c>edit</c>. Two vocabularies for one thing, so the translation lives here rather than in
    /// either of them: widening the persona's enum to accept both spellings would leave the record holding whichever word the
    /// orchestrator happened to use, and a record is read years later by someone who cannot ask which it meant.
    /// </summary>
    private static string DecisionValue(string? decision) => decision switch
    {
        ReviewDecisions.Approved => "approve",
        ReviewDecisions.Rejected => "reject",
        ReviewDecisions.Edited => "edit",
        // Not a default of "approve": the only decisions that exist are the three above, so anything else is a bug, and a bug
        // that writes an approval nobody gave is the worst outcome this workflow has. Fault instead and leave the run visible.
        _ => throw new InvalidOperationException($"HumanReview returned the decision '{decision}', which AdoptionIntake will not record."),
    };

    /// <summary>Triage's own warning that its numbers were soft. It never changes the route - it tells the decider the route is weak evidence.</summary>
    private static string Soft(string? triageJson) =>
        Bool(triageJson, "lowConfidence") ? " (low confidence: a person should look)" : string.Empty;

    /// <summary>What the decider reads. Everything the pipeline learned, shortest-first, with what is missing never more than a glance away.</summary>
    private static string Brief(string? requestJson, string? triageJson, string? formJson, string? reviewJson)
    {
        var brief = new StringBuilder();
        brief.Append("# ").AppendLine(Str(requestJson, "title") ?? "Adoption request");
        if (Str(requestJson, "requestedBy") is { Length: > 0 } by)
        {
            brief.Append("Requested by ").AppendLine(by);
        }

        brief.AppendLine().AppendLine("## Triage")
            .Append("Route **").Append(Str(triageJson, "route") ?? "unknown").Append("**, scrutiny ")
            .Append(Num(triageJson, "scrutiny")?.ToString("0.#", CultureInfo.InvariantCulture) ?? "-").Append(", completeness ")
            .Append(Num(triageJson, "completeness")?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-").AppendLine(".");
        if (Bool(triageJson, "lowConfidence"))
        {
            brief.AppendLine("**Low confidence**: at least one of the numbers above came back under 0.6. The route still followed the rule.");
        }

        if (Str(triageJson, "note") is { Length: > 0 } note)
        {
            brief.AppendLine(note);
        }

        brief.AppendLine().AppendLine("## The request, as a form");
        brief.AppendLine(Str(formJson, "summary") ?? "(no summary)");
        var missing = Strings(formJson, "missing");
        brief.Append("**Not stated:** ").AppendLine(missing.Count == 0 ? "nothing - every field was answered." : string.Join(", ", missing));

        if (reviewJson is not null)
        {
            brief.AppendLine().AppendLine("## Review");
            brief.Append("Verdict **").Append(Str(reviewJson, "verdict") ?? "-").Append("**, ")
                .Append(Int(reviewJson, "blockers") ?? 0).Append(" blocker(s), ")
                .Append(Int(reviewJson, "openQuestions") ?? 0).AppendLine(" open question(s).");
            brief.AppendLine(Str(reviewJson, "summary") ?? string.Empty);
            var overlaps = Strings(reviewJson, "overlapsWith");
            if (overlaps.Count > 0)
            {
                brief.Append("**Overlaps:** ").AppendLine(string.Join(", ", overlaps));
            }

            if (Str(reviewJson, "report") is { Length: > 0 } report)
            {
                brief.Append("Full report: ").AppendLine(report);
            }
        }

        brief.AppendLine().AppendLine("---")
            .AppendLine("Approve, reject, or edit with conditions. Whatever you write in the comment becomes the record, so conditions belong there.");
        return brief.ToString();
    }

    /// <summary>Everything the pipeline learned, flattened into the record persona's contract.</summary>
    private static Dictionary<string, object?> RecordInput(
        string? requestJson, string? triageJson, string? formJson, string? reviewJson,
        string? decision, string? comment, string? decidedBy)
    {
        var input = Pick(formJson, "tool", "vendor", "category", "cost", "dataHandled", "usersAffected", "missing")
            .With("title", Str(requestJson, "title"))
            .With("requestedBy", Str(requestJson, "requestedBy") ?? Str(formJson, "requestedBy"))
            .With("route", Str(triageJson, "route") ?? "full-review")
            .With("scrutiny", Num(triageJson, "scrutiny"))
            .With("triageNote", Str(triageJson, "note"))
            .With("decision", DecisionValue(decision))
            .With("decisionComment", comment)
            .With("decidedBy", decidedBy)
            .With("decidedAt", DateTimeOffset.UtcNow.ToString("O"));

        // lowConfidence is written even when false: "triage was sure" and "nobody recorded it" are different claims, and the
        // record is read years later by someone who cannot tell them apart from an absent field.
        input["lowConfidence"] = Bool(triageJson, "lowConfidence");

        if (reviewJson is not null)
        {
            input.With("reviewVerdict", Str(reviewJson, "verdict"))
                .With("reviewSummary", Str(reviewJson, "summary"))
                .With("openQuestions", Int(reviewJson, "openQuestions"))
                .With("blockers", Int(reviewJson, "blockers"));
            if (Prop(reviewJson, "overlapsWith") is { ValueKind: JsonValueKind.Array } overlaps)
            {
                input["overlapsWith"] = overlaps;
            }
        }

        return input;
    }
}
