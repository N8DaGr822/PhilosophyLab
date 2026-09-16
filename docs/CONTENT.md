# Content, scoring, and historical results

`wwwroot/data/test.schema.json` describes the JSON shape. `CatalogValidator` is the runtime and CI authority for semantic rules: unique IDs, known dimensions/contexts, valid bounds, option/row references, and dependencies on earlier steps.

## Scoring contract

- Choice: the selected option's effects.
- Pick: sum selected option weights and divide by `pickCount`. Pick option weights may have magnitude up to `pickCount`; the final dimension contribution is clamped to [-1, 1]. This preserves the original lifeboat friend weight of 3 with six picks, contributing 0.5 to partiality.
- Allocation: average the dimension-weighted deviations from equal shares, clamped per option. `step` is the button increment and input hint; any nonnegative integer allocation summing to the pool is valid, including legacy answers.
- Scale: map min/middle/max to -1/0/+1, multiplied by the effect.
- Ladder: refusal is -1, the top rung +1, intermediate rungs evenly spaced, multiplied by the effect.
- Assign: average the row effects for chosen people, for rows affecting the dimension.

Except for pick weights, effects must be in [-1, 1]. Each answered question contributes one unit of weight per dimension it can affect. Missing answers contribute nothing. A choice with no effects can still contribute descriptive strategy evidence.

Example: for a 1–7 scale with `learning: -1`, answers 1, 4, 7 contribute +1, 0, -1. The automated checks also verify the lifeboat weighted-pick example and round-trip every current test's answers and snapshot.

## Evidence and connected steps

Options may contain `strategies`, an array of descriptive approach names. Evidence reports the number of selections divided by the number of answered choice questions where that approach was offered. Tags can overlap, so totals need not sum to 100%. Use consistent tag names across comparable questions. Counts do not establish ability or generalize beyond these scenarios.

A question may include ordered `variants`:

```json
{
  "questionId": "design",
  "optionId": "sketch",
  "text": "You followed your sketch. In this simulation, a joint twisted."
}
```

The first matching variant supplies the situation text. Use `rowId` as well to depend on an earlier assignment. If none match, `scenario` is shown. References must point backward to a choice or assignment; dynamic Growth subsets cannot contain dependent steps. All questions are still presented. If an earlier response changes in a connected sequence, later answers are cleared on Next so they cannot be saved against an unseen consequence. Back/Next without changing an answer preserves it.

## Interpretation and versions

New results retain the exact test definition and Growth comparison baseline from the attempt. Result recaps and behavior evidence use the snapshot. Legacy results without snapshots are interpreted only when their version matches the current catalog; otherwise the raw answers remain available and the result is excluded from current profile interpretation. Do not silently reinterpret a dimension: use a new ID if its meaning changes.

Growth compares responses against a suggested direction and labels the result as agreement with that suggestion. It does not score real-world success. New history uses the saved baseline; legacy history without a baseline is explicitly described as using current preferences. The overall profile reflects the current collection of latest results.

Repeated axis preferences require at least three contributing baseline responses. Context comparisons require at least two responses in each group. These thresholds reduce sparse summaries; they are not statistical validation. Strategy counts always show their denominator, including small samples.
