# Template Generator — Validation Engine Design

> Companion to [template-generator-design.md](template-generator-design.md). That document covers the high-level architecture (inputs, sections, store, hosting). This one details the **validation engine** — the catalog, the applicability matrix, and the **Factory + Strategy** implementation that makes the rule set extensible.

## Two validation concerns

The engine serves two distinct concerns with the **same** rule strategies:

1. **Authoring/structural validation** — when a template is saved or published, is each rule _applicable_ to the input it's attached to, and are its parameters well-formed? (e.g. reject `AtLeast` on a `Text` field; reject `Min > Max`.) This is the in-scope path, invoked by `SaveTemplate` / `PublishTemplate`.
2. **Response/runtime validation** — when an end user fills a template, the UI **dynamically translates the rule set** for client-side validation (the `validations[]` array in the template payload is the schema the UI reads directly). When the form is submitted, the backend **re-validates** via `ISubmissionValidator` — same strategies, `Evaluate` path — before persisting the `FormSubmission`. Double-validation means the server is the authority even if client-side validation is bypassed.

Each `IValidationRule` strategy therefore exposes both a `ValidateDefinition` (concern 1) and an `Evaluate` (concern 2) method.

---

## Validation rule catalog & applicability matrix

Each input **category** permits a specific set of rule kinds. The validator enforces this matrix at authoring time — a rule attached to an input it doesn't apply to is a structural error.

| Rule kind    |    Text    |  Numeric  | Checkbox | SingleSelect | MultiSelect | Parameters                   |
| ------------ | :--------: | :-------: | :------: | :----------: | :---------: | ---------------------------- |
| `Required`   |     ✓      |     ✓     |    ✓     |      ✓       |      ✓      | —                            |
| `Min`        | ✓ (length) | ✓ (value) |          |              |             | `value`                      |
| `Max`        | ✓ (length) | ✓ (value) |          |              |             | `value`                      |
| `Pattern`    |     ✓      |           |          |              |             | `regex`                      |
| `AtLeast`    |            |           |          |              |      ✓      | `value` (min selections)     |
| `AtMost`     |            |           |          |              |      ✓      | `value` (max selections)     |
| `RequiredIf` |     ✓      |     ✓     |    ✓     |      ✓       |      ✓      | `field`, `operator`, `value` |

> `Min`/`Max` are **semantically interpreted by the strategy per category** — length for `Text`, magnitude for `Numeric`. The rule _kind_ stays generic; the strategy resolved by the factory knows how to read it for the input it's attached to. This keeps the authoring payload simple while still validating correctly. (If you prefer explicit kinds, split into `MinLength`/`MaxLength` vs `MinValue`/`MaxValue` — the factory pattern accommodates either choice with no ripple.)

The matrix itself is **not** a central table the validator owns — each strategy declares which categories it supports via `AppliesTo`, so a new rule's applicability ships with the rule and can't drift out of sync.

---

## Design: Factory + Strategy

Two abstractions, both in `TemplateGenerator.Domain` (validation is domain logic):

```
ITemplateValidator (domain service)
   │  walks template → for each input → for each rule:
   │
   └── IValidationRuleFactory.Resolve(kind) ──► IValidationRule  (Strategy)
                                                   ├── RequiredRule
                                                   ├── MinRule / MaxRule
                                                   ├── PatternRule
                                                   ├── AtLeastRule / AtMostRule
                                                   └── RequiredIfRule   (custom, cross-field)
```

### 1. `IValidationRule` (Strategy)

One implementation per rule **kind**. Each strategy declares the input categories it supports, validates its own parameters at authoring time, and evaluates an answer at response time.

```csharp
public interface IValidationRule
{
    // Discriminator the factory keys on, e.g. "Required", "Min", "AtLeast".
    string Kind { get; }

    // Authoring-time applicability — owns its own row of the matrix.
    bool AppliesTo(InputType inputType);

    // Authoring-time: are the rule's params well-formed for this input?
    // (e.g. Min must be numeric; Pattern must compile; Min <= sibling Max.)
    RuleValidationResult ValidateDefinition(InputDefinition input, RuleDefinition rule);

    // Response-time: evaluate a user's answer. (Future scope, same strategy.)
    RuleEvaluationResult Evaluate(EvaluationContext context, RuleDefinition rule);
}
```

`EvaluationContext` exposes the answer under evaluation **and** sibling answers (keyed by `key`), which is what cross-field rules like `RequiredIf` need.

### 2. `IValidationRuleFactory` (Factory)

Resolves a `RuleDefinition` (carrying its `Kind`) to the matching strategy. It is a pure `Kind → strategy` index built from the set of registered `IValidationRule` instances.

```csharp
public interface IValidationRuleFactory
{
    IValidationRule Resolve(string kind);     // throws UnknownRuleException if unregistered
    bool IsKnown(string kind);
}

public sealed class ValidationRuleFactory : IValidationRuleFactory
{
    private readonly IReadOnlyDictionary<string, IValidationRule> _byKind;

    // The host's composition root supplies the collection; this is just an index.
    public ValidationRuleFactory(IEnumerable<IValidationRule> rules) =>
        _byKind = rules.ToDictionary(r => r.Kind, StringComparer.OrdinalIgnoreCase);

    public bool IsKnown(string kind) => _byKind.ContainsKey(kind);

    public IValidationRule Resolve(string kind) =>
        _byKind.TryGetValue(kind, out var rule)
            ? rule
            : throw new UnknownRuleException(kind);
}
```

```csharp
// Composition-root wiring (in the Api host) — these are all Domain types;
// listing them in the container is just wiring. Adding a rule is one line here + one class.
services.AddSingleton<IValidationRule, RequiredRule>();
services.AddSingleton<IValidationRule, MinRule>();
services.AddSingleton<IValidationRule, MaxRule>();
services.AddSingleton<IValidationRule, AtLeastRule>();
services.AddSingleton<IValidationRule, AtMostRule>();
services.AddSingleton<IValidationRule, PatternRule>();
services.AddSingleton<IValidationRule, RequiredIfRule>();
services.AddSingleton<IValidationRuleFactory, ValidationRuleFactory>();
```

### 3. Example strategies

**A rule that applies to `Text` — `MaxRule`.** It also shows the per-category interpretation (max _length_ for `Text`, max _value_ for `Numeric`) and a cross-rule sanity check against a sibling `Min`.

```csharp
// Catalog rule "Max": upper bound. Length for Text, magnitude for Numeric.
public sealed class MaxRule : IValidationRule
{
    public string Kind => "Max";

    // Owns its own row of the applicability matrix.
    public bool AppliesTo(InputType inputType) =>
        inputType is InputType.Text or InputType.Numeric;

    // Authoring-time: params well-formed, and not contradicting a sibling Min.
    public RuleValidationResult ValidateDefinition(InputDefinition input, RuleDefinition rule)
    {
        if (!rule.Params.TryGetDecimal("value", out var max))
            return RuleValidationResult.Error("Max requires a numeric 'value' parameter.");

        if (input.TryGetRule("Min", out var min) &&
            min.Params.TryGetDecimal("value", out var minValue) && minValue > max)
        {
            return RuleValidationResult.Error($"Min ({minValue}) must not exceed Max ({max}).");
        }

        return RuleValidationResult.Ok();
    }

    // Response-time: empty answers are Required's job, not Max's.
    public RuleEvaluationResult Evaluate(EvaluationContext ctx, RuleDefinition rule)
    {
        if (ctx.Answer is null) return RuleEvaluationResult.Pass();

        var max = rule.Params.GetDecimal("value");
        decimal actual = ctx.Input.InputType switch
        {
            InputType.Text    => ((string)ctx.Answer).Length,   // length
            InputType.Numeric => Convert.ToDecimal(ctx.Answer),  // magnitude
            _                 => 0m
        };

        return actual <= max
            ? RuleEvaluationResult.Pass()
            : RuleEvaluationResult.Fail(rule.Message ?? $"Must be at most {max}.");
    }
}
```

**A custom cross-field rule — `RequiredIfRule`.** It applies to any input, validates that the referenced field exists, and at response time reads a _sibling_ answer through `EvaluationContext`.

```csharp
// Custom rule "RequiredIf": required only when another field satisfies operator/value.
public sealed class RequiredIfRule : IValidationRule
{
    public string Kind => "RequiredIf";

    public bool AppliesTo(InputType inputType) => true;   // anything can be conditionally required

    public RuleValidationResult ValidateDefinition(InputDefinition input, RuleDefinition rule)
    {
        if (!rule.Params.TryGetString("field", out var field))
            return RuleValidationResult.Error("RequiredIf needs a 'field' parameter.");
        if (field == input.Key)
            return RuleValidationResult.Error("RequiredIf cannot reference its own field.");
        if (!input.Template.ContainsKey(field))
            return RuleValidationResult.Error($"RequiredIf references unknown field '{field}'.");
        if (!rule.Params.Contains("operator") || !rule.Params.Contains("value"))
            return RuleValidationResult.Error("RequiredIf needs 'operator' and 'value' parameters.");

        return RuleValidationResult.Ok();
    }

    public RuleEvaluationResult Evaluate(EvaluationContext ctx, RuleDefinition rule)
    {
        var field    = rule.Params.GetString("field");
        var op       = rule.Params.GetString("operator");
        var expected = rule.Params.GetValue("value");
        var other    = ctx.SiblingAnswer(field);            // cross-field lookup

        var conditionMet = op switch
        {
            "equals"   => Equals(other, expected),
            "contains" => other is IEnumerable<object> set && set.Contains(expected),
            _          => false
        };

        if (!conditionMet) return RuleEvaluationResult.Pass();   // condition off → no requirement

        var provided = ctx.Answer is not null && !Equals(ctx.Answer, "");
        return provided
            ? RuleEvaluationResult.Pass()
            : RuleEvaluationResult.Fail(rule.Message ?? $"Required when '{field}' {op} '{expected}'.");
    }
}
```

### 4. `ITemplateValidator` (orchestrator / domain service)

Walks the template definition; for every input's every rule it `Resolve`s the strategy, checks `AppliesTo`, then `ValidateDefinition`, accumulating pinpointed errors (with `path`/`key`/`rule`). This is what `SaveTemplate` / `PublishTemplate` call.

```csharp
public sealed class TemplateValidator : ITemplateValidator
{
    private readonly IValidationRuleFactory _factory;
    public TemplateValidator(IValidationRuleFactory factory) => _factory = factory;

    public TemplateValidationResult Validate(Template template)
    {
        var errors = new List<TemplateError>();

        foreach (var (section, si) in template.Sections.Indexed())
        foreach (var (entry, ei) in section.Entries.Indexed())
        {
            if (entry is not InputDefinition input) continue;   // Info entries carry no rules
            var path = $"sections[{si}].entries[{ei}]";

            foreach (var rule in input.Validations)
            {
                // Unknown kind?
                if (!_factory.IsKnown(rule.Kind))
                {
                    errors.Add(new(path, input.Key, rule.Kind, $"Unknown rule '{rule.Kind}'."));
                    continue;
                }

                var strategy = _factory.Resolve(rule.Kind);

                // Applicable to this input category?
                if (!strategy.AppliesTo(input.InputType))
                {
                    errors.Add(new(path, input.Key, rule.Kind,
                        $"{rule.Kind} is not applicable to a {input.InputType}."));
                    continue;
                }

                // Params well-formed?
                var result = strategy.ValidateDefinition(input, rule);
                if (!result.IsValid)
                    errors.Add(new(path, input.Key, rule.Kind, result.Message!));
            }
        }

        return new TemplateValidationResult(errors);
    }
}
```

The accumulated `TemplateError` list maps directly to the `400` response body shown in the main design doc (`path` / `key` / `rule` / `message`).

### 5. `ISubmissionValidator` (orchestrator / domain service)

The runtime counterpart to `TemplateValidator`. Called by the `SubmitForm` use case, it walks the template's inputs, maps each submitted answer into an `EvaluationContext` (including all sibling answers for cross-field rules like `RequiredIf`), and calls `Evaluate` on each rule strategy.

```csharp
public interface ISubmissionValidator
{
    SubmissionValidationResult Validate(Template template, FormSubmission submission);
}

public sealed class SubmissionValidator : ISubmissionValidator
{
    private readonly IValidationRuleFactory _factory;
    public SubmissionValidator(IValidationRuleFactory factory) => _factory = factory;

    public SubmissionValidationResult Validate(Template template, FormSubmission submission)
    {
        var errors = new List<SubmissionFieldError>();

        foreach (var (section, _) in template.Sections.Indexed())
        foreach (var (entry, _)   in section.Entries.Indexed())
        {
            if (entry is not InputDefinition input) continue;

            var answer = submission.Answers.TryGetValue(input.Key, out var a) ? a : null;
            var ctx    = new EvaluationContext(input, answer, submission.Answers);

            foreach (var rule in input.Validations)
            {
                if (!_factory.IsKnown(rule.Kind)) continue;   // unknown kinds caught at authoring time

                var strategy = _factory.Resolve(rule.Kind);
                if (!strategy.AppliesTo(input.InputType)) continue;

                var result = strategy.Evaluate(ctx, rule);
                if (!result.IsValid)
                    errors.Add(new(input.Key, rule.Kind, result.Message!));
            }
        }

        return new SubmissionValidationResult(errors);
    }
}
```

`EvaluationContext` receives `submission.Answers` (the full key→value map) as its sibling-answers dictionary, which is what cross-field rules like `RequiredIfRule` need via `ctx.SiblingAnswer(field)`.

The accumulated `SubmissionFieldError` list maps to the `422 Unprocessable Entity` body returned by `POST /api/v1/templates/{id}/submissions`:

```json
{
  "error": "SUBMISSION_INVALID",
  "errors": [
    { "key": "driver_age",       "rule": "Min",        "message": "Must be at least 16." },
    { "key": "coverage_options", "rule": "AtLeast",    "message": "Select at least one coverage option." },
    { "key": "accident_details", "rule": "RequiredIf", "message": "Please describe the accidents you reported." }
  ]
}
```

---

## Supporting types

Small domain value objects the strategies lean on, kept deliberately thin:

| Type                     | Purpose                                                                                              |
| ------------------------ | ---------------------------------------------------------------------------------------------------- |
| `RuleDefinition`         | Persisted rule: `Kind`, `Params` (typed bag), optional `Message`.                                    |
| `RuleParams`             | Convenience bag over the params: `TryGetDecimal`, `GetString`, `Contains`, `GetValue`, …             |
| `InputDefinition`        | The input being validated: `Key`, `InputType`, `Validations`, plus `TryGetRule(kind)` and `Template` (sibling-key lookup). |
| `EvaluationContext`      | Response-time: `Answer`, `Input`, and `SiblingAnswer(key)` for cross-field rules.                    |
| `RuleValidationResult`   | Authoring outcome: `Ok()` / `Error(message)`.                                                        |
| `RuleEvaluationResult`   | Response outcome: `Pass()` / `Fail(message)`.                                                        |
| `TemplateValidationResult`   | Aggregated authoring outcome: list of `TemplateError(path, key, rule, message)`.                   |
| `FormSubmission`             | Runtime aggregate: `Answers` (key → value map, where value is `string\|number\|bool\|string[]`).   |
| `SubmissionFieldError`       | Runtime field error: `Key`, `Rule`, `Message`.                                                     |
| `SubmissionValidationResult` | Aggregated runtime outcome: list of `SubmissionFieldError`.                                        |

---

## Why this shape

- **Open/closed.** A new rule = a new `IValidationRule` class + one DI line. No edits to the validator, the factory, the use cases, or the store.
- **Applicability lives with the rule.** The matrix isn't a central `switch` that every new rule must edit — each strategy owns its own `AppliesTo`, so it can't drift out of sync.
- **One engine, two phases.** The same strategy serves authoring (`ValidateDefinition`) and the future response phase (`Evaluate`), so applicability and evaluation logic never disagree.
- **Pure domain.** No infrastructure dependency, so the whole engine unit-tests in isolation with plain objects — no host, no container, no store.

---

## Adding a new rule — worked example (`Range`)

A `Range` rule for `Numeric` (value must fall within `[min, max]`):

1. **Write the strategy** — `RangeRule : IValidationRule`, `Kind => "Range"`, `AppliesTo(Numeric)`, validate `min <= max` in `ValidateDefinition`, bounds check in `Evaluate`.
2. **Register it** — one line: `services.AddSingleton<IValidationRule, RangeRule>();`.
3. **Done.** The factory now resolves `"Range"`, the validator picks it up automatically, and authors can attach it. **Zero** edits to `TemplateValidator`, `ValidationRuleFactory`, the use cases, the contracts, or the store.

---

## Testing the validator

1. **Strategy unit tests (in isolation).** For each `IValidationRule`: assert `AppliesTo` matches the matrix (e.g. `AtLeastRule.AppliesTo(Text) == false`); `ValidateDefinition` rejects bad params (`Min > Max`, uncompilable `Pattern`, `RequiredIf` pointing at a missing/own `key`); `Evaluate` returns correct pass/fail for representative answers, including cross-field cases via a stub `EvaluationContext`.
2. **Factory tests.** `Resolve` returns the right strategy per kind; unknown kind throws `UnknownRuleException`; `IsKnown` is correct; every registered strategy has a unique `Kind`.
3. **`TemplateValidator` tests.** Feed whole template definitions (the insurance-quote sample, plus deliberately broken variants) and assert the accumulated error list has the right `path` / `key` / `rule` for each violation.
4. **`SubmissionValidator` tests.** Feed a valid template + answer maps: assert `Pass` on valid submissions; assert the correct `key`/`rule`/`message` for each rule violation (missing required field, out-of-range numeric, `RequiredIf` triggered by a sibling answer, etc.).
5. **Extensibility smoke test.** Add a throwaway `RangeRule`, register it, and confirm it validates end-to-end (both `TemplateValidator` and `SubmissionValidator`) with **zero** edits to either validator, the factory, use cases, or the store — proving the open/closed claim.
