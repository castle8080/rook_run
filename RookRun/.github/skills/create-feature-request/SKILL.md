---
name: create-feature-request
description: "Use when the user wants to turn an idea into a complete feature request and implementation spec. Collect high-level goals with many clarifying questions, inspect the current codebase, propose technical design options, include ASCII UX wireframes when relevant, and generate a tracked feature document at docs/features/{feature-name}.md (fallback: doc/features/{feature-name}.md)."
---

# Create Feature Request Skill

## Purpose

Create a high-quality feature request document from user input by running a structured discovery and design workflow.

Primary output:
- Preferred: `docs/features/{feature-name}.md`
- Fallback: `doc/features/{feature-name}.md`

## Inputs

Collect these inputs if not provided:
- Feature name
- Problem statement
- Desired outcomes
- Constraints (time, risk, dependencies)
- Stakeholders and users
- Success metrics

## Workflow

Follow these phases in order. Do not skip phases.

### Phase 1: Goal Discovery (High-Level)

Start with the user's initial request and ask broad questions to lock down intent before discussing implementation.

Question areas:
- User problem and pain points
- Primary and secondary personas
- In-scope vs out-of-scope
- Acceptance criteria (observable behavior)
- Business value and urgency
- Compliance/security/privacy constraints
- Backward compatibility needs
- Rollout expectations and migration concerns

Rules:
- Ask multiple questions per round.
- Continue until major unknowns are resolved.
- Summarize understanding and request confirmation before moving on.

### Phase 2: Current-State Analysis

Inspect the existing codebase and project docs to ground the feature in current architecture.

Required analysis:
- Existing endpoints/services/components that are related
- Data contracts and models that will be affected
- Jobs/background processing impacts
- Object store or persistence impacts
- Test surface to update

Rules:
- Cite concrete files/symbols in your reasoning.
- If architecture is unclear, ask targeted questions before proposing a design.

### Phase 3: Technical Design

Propose one or more implementation options and discuss tradeoffs.

For each option include:
- Architecture approach
- Data flow
- API/contract changes
- Storage/repository changes
- Failure modes and error handling
- Test strategy
- Complexity/risk estimate

Rules:
- Provide at least 2 options when meaningful.
- When design details depend on current ecosystem practices, libraries, or framework capabilities, search current online materials to confirm the design uses up-to-date techniques and packages.
- Prefer authoritative and current sources such as official documentation, actively maintained library docs, and recent platform guidance.
- Ask follow-up questions to resolve tradeoffs and choose a direction.
- Record final design decisions and rejected alternatives with reasons.

### Phase 4: UX Design (If User-Facing)

If the feature changes user experience, ask UX-specific questions.

Question areas:
- Entry points/navigation
- Key user tasks
- Empty/loading/error states
- Accessibility expectations
- Mobile/desktop differences

Output requirements:
- Include ASCII wireframes for key screens and states.
- Keep wireframes simple and task-oriented.

### Phase 5: Write Feature Spec

Create the feature document using the template at `.github/skills/create-feature-request/templates/feature-request-template.md` and fill all relevant sections.

Template lookup rules:
- Read `.github/skills/create-feature-request/templates/feature-request-template.md` directly.
- Do not search recursively for the template unless that exact file is missing.
- If the template file is missing, stop and report the missing path instead of guessing.

Path selection rules:
- If `docs/features` exists, write to `docs/features/{feature-name}.md`.
- Else if `doc/features` exists, write to `doc/features/{feature-name}.md`.
- Else create `docs/features` and write to `docs/features/{feature-name}.md`.

The spec must include:
- Goal overview
- Project context (what this project is and why this feature fits)
- Acceptance criteria
- Design decisions
- Optional UX design and wireframes
- Implementation plan with trackable status
- Implementer expectations

### Phase 6: Initialize Tracking

Initialize progress tracking sections in the markdown.

Tracking requirements:
- Overall status
- Milestones/phases
- Task checklist
- Decision log
- Implementation notes and lessons learned
- Test plan and evidence
- Code review checklist
- Documentation update checklist

### Phase 7: Save and Confirm

Save the final file using this priority:
- `docs/features/{feature-name}.md`
- `doc/features/{feature-name}.md` (only when that folder already exists and `docs/features` does not)

After writing:
- Share a concise summary of what was captured.
- List open questions that still need answers.
- Suggest immediate next implementation steps.

## Quality Bar

Before finalizing, verify:
- The feature intent is clear and testable.
- Acceptance criteria are concrete and measurable.
- Design decisions are justified.
- Risks and unknowns are explicit.
- Tracking sections are initialized and actionable.
- Implementer expectations include code, tests, docs, and review.

## Behavioral Guidance

- Prefer asking more clarifying questions over making assumptions.
- Keep solution options practical for the current codebase.
- Avoid over-design; focus on a shippable initial version with clear follow-on items.
- If critical information is missing, stop and ask before finalizing the spec.
