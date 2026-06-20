# Feature Workflow

## Clarify First

- Ask clarifying questions before implementation unless the task is trivial.
- Confirm acceptance criteria and out-of-scope items early.

## Plan and Slice

- Break work into small, targeted steps.
- Prefer a vertical slice that can be tested quickly.
- Keep changes localized to relevant projects.

## Task Notes

- Track work in `var/tasks/{task}.md`.
- Keep notes current as work progresses so sessions can resume quickly.
- Include: goal, plan, progress, decisions, open questions, and validation steps.

## Validation

- Test as each step completes.
- Run focused tests first, then broader checks if needed.
- Include static analysis/security/clean-code review as part of completion.

## AI Reviewer Stage

- Run an AI review pass after implementation and before final sign-off.
- Treat AI self-check as a first-pass filter only, not a final approval.
- Use an independent reviewer context/rubric when possible to reduce authoring bias.
- Require severity-based findings output (Critical/High/Medium/Low).
- Prioritize fixing Critical and High findings, then reassess remaining items.
- Keep human or domain review for non-trivial and higher-risk changes.

Reference:

- `.github/instructions/ai-review.instructions.md`

## Completion Criteria

- Behavior implemented and verified.
- Relevant tests added/updated and passing.
- AI review completed and high-severity findings addressed.
- Notes updated with outcomes and follow-ups.
- Documentation updated when conventions or architecture changed.
