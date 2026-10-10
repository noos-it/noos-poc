using System.Text.Json;
using Elsa.Workflows;
using Noos.Orchestrator.Tests.Infrastructure;

namespace Noos.Orchestrator.Tests;

/// <summary>
/// AdoptionIntake against the fake gateway. What is worth asserting here is not that four personas ran, but that the route
/// decided which ones did, that nothing was invented on the way between them, and that the decision reached the record.
/// </summary>
public class AdoptionIntakeTests : IAsyncLifetime
{
    private FakeGateway gateway = null!;
    private OrchestratorHost host = null!;

    public async ValueTask InitializeAsync()
    {
        gateway = await FakeGateway.StartAsync();
        gateway.Personas.AddRange(["request-triage@1.0.1", "request-extract@1.0.0", "product-selection-review@1.3.0", "decision-record@1.0.0"]);
        host = new OrchestratorHost(gateway, "noostest" + Guid.NewGuid().ToString("N"));
    }

    public async ValueTask DisposeAsync()
    {
        await host.DisposeAsync();
        await gateway.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static string Name(FakeJob j) => j.Persona.Split('@')[0];

    private const string Request = """{"title":"Adopt Linear","body":"Jira is slow. Linear is nicer. $8/user/month, 40 people.","requestedBy":"ada"}""";

    private static FakeReply Triage(string route, string category = "collaboration", double scrutiny = 4, bool lowConfidence = false) =>
        FakeReply.OkJson($$"""
            {"route":"{{route}}","completeness":0.8,"category":"{{category}}","scrutiny":{{scrutiny}},
             "lowConfidence":{{(lowConfidence ? "true" : "false")}},"note":"triage note"}
            """);

    private static FakeReply Form(bool reviewable = true) => FakeReply.OkJson($$"""
        {"tool":"Linear","category":"collaboration","problem":"Jira is slow","reasoning":"{{(reviewable ? "measured on a branch" : "")}}",
         "cost":"$8/user/month","dataHandled":"unknown","usersAffected":40,
         "missing":["vendor","alternativesConsidered","dataHandled"],
         "quoted":[{"field":"tool","text":"Linear"}],
         "reviewable":{{(reviewable ? "true" : "false")}},"summary":"Asks for Linear; does not say what data it holds."}
        """);

    private static FakeReply Review() => FakeReply.OkJson("""
        {"report":"review.html","verdict":"adopt-with-conditions","overlapsWith":["Azure Boards"],
         "openQuestions":2,"blockers":0,"summary":"Reasonable, overlaps Azure Boards."}
        """);

    private static FakeReply Record() => FakeReply.OkJson("""
        {"record":"decision.md","outcome":"adopted-with-conditions",
         "inventoryDelta":{"action":"add","capability":"Issue tracking","tool":"Linear","supersedes":[]},
         "summary":"Adopted with conditions."}
        """);

    private FakeJob JobOf(string persona) => gateway.JobsOf(persona).Single();

    private static JsonElement Input(FakeJob j, string property) => j.Input.GetProperty(property);

    [Fact]
    public async Task Full_review_route_runs_the_reviewer_and_carries_the_form_into_it_without_the_extractors_own_fields()
    {
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => Triage("full-review"),
            "request-extract" => Form(),
            "product-selection-review" => Review(),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the human decision");

        // Triage saw the request as written, not a goal string built from it.
        Input(JobOf("request-triage"), "body").GetString().Should().Be("Jira is slow. Linear is nicer. $8/user/month, 40 people.");

        // The category triage decided reached the extractor verbatim: the two enums are the same twelve values for this reason.
        Input(JobOf("request-extract"), "category").GetString().Should().Be("collaboration");

        // The reviewer got the form's fields and ONLY those: its schema is additionalProperties:false, so `missing`,
        // `quoted`, `reviewable` or `summary` leaking through would have the gateway reject the job.
        var reviewJob = JobOf("product-selection-review");
        reviewJob.Input.EnumerateObject().Select(p => p.Name).Should()
            .BeEquivalentTo(["tool", "category", "problem", "reasoning", "cost", "dataHandled", "usersAffected"]);

        // The decider is handed the verdict and what nobody answered.
        var review = (await host.PendingReviewsAsync(id)).Single();
        review.Title.Should().Contain("adopt-with-conditions");
        review.Content.Should().Contain("Azure Boards").And.Contain("Not stated:").And.Contain("dataHandled");
    }

    [Fact]
    public async Task The_decision_and_everything_known_at_the_time_reach_the_record()
    {
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => Triage("full-review", lowConfidence: true),
            "request-extract" => Form(),
            "product-selection-review" => Review(),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the human decision");
        var review = (await host.PendingReviewsAsync(id)).Single();
        review.Title.Should().Contain("low confidence");

        (await host.DecideAsync(review.Id, "approve", comment: "Fine, but security signs off the DPA first.")).EnsureSuccessStatusCode();
        await host.WaitForSubStatusAsync(id, WorkflowSubStatus.Finished);

        var record = JobOf("decision-record").Input;
        // HumanReview says "Approved"; decision-record's contract says "approve". The translation is the workflow's.
        record.GetProperty("decision").GetString().Should().Be("approve");
        record.GetProperty("decisionComment").GetString().Should().Contain("DPA");
        record.GetProperty("route").GetString().Should().Be("full-review");
        record.GetProperty("reviewVerdict").GetString().Should().Be("adopt-with-conditions");
        record.GetProperty("tool").GetString().Should().Be("Linear");
        // Written even though it is false-by-default elsewhere: "triage was sure" and "nobody recorded it" are different claims.
        record.GetProperty("lowConfidence").GetBoolean().Should().BeTrue();
        record.GetProperty("missing").EnumerateArray().Select(x => x.GetString()).Should().Contain("dataHandled");

        var log = await host.StepLogAsync(id);
        log.Select(l => l.StepName).Should().ContainInOrder("triage", "extract", "review", "decision", "record");
        log.Should().OnlyContain(l => l.CorrelationId == id);
    }

    [Fact]
    public async Task Fast_track_skips_the_expensive_reviewer_but_still_asks_a_person()
    {
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => Triage("fast-track", scrutiny: 2),
            "request-extract" => Form(),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the sign-off");

        gateway.JobsOf("product-selection-review").Should().BeEmpty();
        (await host.PendingReviewsAsync(id)).Single().Title.Should().Contain("Sign-off");

        var review = (await host.PendingReviewsAsync(id)).Single();
        (await host.DecideAsync(review.Id, "approve")).EnsureSuccessStatusCode();
        await host.WaitForSubStatusAsync(id, WorkflowSubStatus.Finished);

        // No review ran, so the record is told nothing about one rather than being told it was empty.
        var record = JobOf("decision-record").Input;
        record.TryGetProperty("reviewVerdict", out _).Should().BeFalse();
        record.GetProperty("route").GetString().Should().Be("fast-track");
    }

    [Fact]
    public async Task Send_back_still_fills_the_form_because_the_missing_fields_are_what_goes_back()
    {
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => Triage("send-back"),
            "request-extract" => Form(reviewable: false),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the send-back");

        gateway.JobsOf("request-extract").Should().ContainSingle();
        gateway.JobsOf("product-selection-review").Should().BeEmpty();
        var review = (await host.PendingReviewsAsync(id)).Single();
        review.Title.Should().Contain("Send back");
        review.Content.Should().Contain("vendor").And.Contain("alternativesConsidered");
    }

    [Fact]
    public async Task A_full_review_route_with_nothing_reviewable_goes_to_the_person_instead_of_the_reviewer()
    {
        // The route says review it; the form says there is no tool, problem or reasoning to review. product-selection-review
        // requires those three, so running it would buy a page of questions at the price of the expensive model.
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => Triage("full-review", scrutiny: 5),
            "request-extract" => Form(reviewable: false),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the decision without a review");

        gateway.JobsOf("product-selection-review").Should().BeEmpty();
        (await host.PendingReviewsAsync(id)).Single().Title.Should().Contain("without a review");
    }

    [Fact]
    public async Task A_triage_answer_that_is_not_its_schema_routes_to_the_review_rather_than_past_it()
    {
        // The expensive path is the safe default: a request skipped past review because a persona answered with prose is the
        // one failure here that nobody would notice.
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => FakeReply.Ok("I think this one looks fine honestly"),
            "request-extract" => Form(),
            "product-selection-review" => Review(),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the human decision");

        gateway.JobsOf("product-selection-review").Should().ContainSingle();
        // Nothing was invented to fill the gap: no category reached the extractor at all.
        JobOf("request-extract").Input.TryGetProperty("category", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("reject", "reject")]
    [InlineData("edit", "edit")]
    [InlineData("approve", "approve")]
    public async Task Every_decision_reaches_the_record_in_the_records_own_vocabulary(string decided, string recorded)
    {
        gateway.Responder = j => Name(j) switch
        {
            "request-triage" => Triage("fast-track", scrutiny: 2),
            "request-extract" => Form(),
            "decision-record" => Record(),
            _ => FakeReply.Fail("unexpected"),
        };

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", Request);
        await host.WaitForAsync(async () => (await host.PendingReviewsAsync(id)).Count == 1, "the sign-off");
        var review = (await host.PendingReviewsAsync(id)).Single();

        (await host.DecideAsync(review.Id, decided, edited: decided == "edit" ? "with conditions" : null, comment: "because")).EnsureSuccessStatusCode();
        await host.WaitForSubStatusAsync(id, WorkflowSubStatus.Finished);

        JobOf("decision-record").Input.GetProperty("decision").GetString().Should().Be(recorded);
    }

    [Fact]
    public async Task A_request_without_a_body_is_refused_before_any_persona_runs()
    {
        gateway.Responder = _ => FakeReply.Fail("should not run");

        var id = await host.StartTaskAsync("AdoptionIntake", "Adopt Linear", """{"title":"Adopt Linear"}""");
        await host.WaitForSubStatusAsync(id, WorkflowSubStatus.Faulted);

        gateway.Jobs.Should().BeEmpty();
    }
}
