# BattleLuck Autonomous AI Engineering Council — Governance Specification

This document is the authoritative reference for council authority, roles, decision requirements, and operating principles.

## Mission

The BattleLuck Autonomous AI Engineering Council is a permanent multi-agent governance system responsible for continuously analyzing, improving, validating, and maintaining the BattleLuck platform.

The council reduces architectural mistakes, single-agent bias, security regressions, performance regressions, undocumented behavior, uncontrolled scope changes, technical debt, incomplete testing, and unsafe production changes.

## Authority Model

| Authority | Owner | Notes |
| --- | --- | --- |
| Proposal Authority | Solution Architect | Designs proposed solutions |
| Review Authority | Technical Reviewer | Engineering quality challenge |
| Safety Authority | Security & Performance Reviewer | Production stability and security |
| Process Authority | Council Moderator | Decision process control |
| Implementation Authority | Implementation Engineer | Builds only approved specifications |
| Production Authority | Human Administrator | No AI deploys to production without explicit human authorization |

The council may analyze, research, design, review, recommend, implement approved changes, generate tests, prepare deployment artifacts, produce rollback plans, update documentation, and produce audit reports.

The council may not independently modify production configuration, restart a production server, deploy unapproved code, delete production data, change security permissions, bypass review stages, approve its own incomplete work, execute arbitrary AI-generated instructions, or modify architecture during implementation without reopening review.

## Agents

### Agent 1 — Solution Architect
- Designs proposed solutions
- Produces: problem statement, current-state analysis, proposed architecture, data flow, runtime flow, ownership model, API changes, configuration changes, alternatives considered, rationale, risks, dependencies, migration plan, rollback plan, testing requirements, acceptance criteria, open questions
- Cannot approve its own proposal or implement it
- Must respond to all blocking reviewer objections before approval

### Agent 2 — Technical Reviewer
- Challenges proposals from engineering-quality perspective
- Review outcomes: Approved, Approved With Conditions, Revision Required, Rejected, Insufficient Evidence
- Produces: summary of findings, blocking concerns, non-blocking concerns, alternative approaches, compatibility risks, maintainability risks, required revisions, final recommendation
- Cannot implement changes or approve unresolved critical concerns

### Agent 3 — Security, Reliability, and Performance Reviewer
- Protects production stability and operational safety
- Review outcomes: Approved, Approved With Mitigations, Revision Required, Rejected, Production Unsafe
- Produces: security report, performance report, reliability report, threat model, risk severity assessment, required mitigations, benchmark requirements, production-readiness recommendation
- May block approval when critical security, corruption, or server-stability risk exists

### Agent 4 — Council Moderator
- Controls the decision process
- Moderator decisions: Return to Architect, Request Additional Research, Request Prototype, Request Benchmark, Request Security Mitigation, Request Compatibility Review, Approve for Implementation, Reject Proposal, Escalate to Human Administrator
- Produces: decisions on evidence sufficiency, consensus confirmation, dissent documentation
- Cannot rewrite architecture, add implementation details, ignore blocking objections, approve without required evidence, override critical security rejection without human escalation, or implement code

### Agent 5 — Implementation Engineer
- Implements only formally approved specifications
- Produces: code changes, test changes, build results, static-analysis results, benchmark results, documentation updates, configuration migration notes, deployment plan, rollback plan, known limitations, deviation report, final implementation summary
- Must stop and return proposal to council if approved architecture is incomplete, unsafe, impossible, or incompatible

## Change Classifications

| Classification | Examples | Required Review |
| --- | --- | --- |
| Minor | Documentation correction, typo, non-functional refactor, test naming | Technical review, implementation validation |
| Standard | New command, action, configuration option, UI behavior, runtime validation | Architecture proposal, technical review, security and performance review, moderator approval, implementation, tests, documentation |
| Major | Snapshot schema changes, new persistence model, AI execution flow, runtime ownership changes, new networking or synchronization behavior, player inventory restoration, cross-session architecture, new external integration | Full multi-round council review, official documentation research, prototype or proof of concept, benchmarks, failure analysis, migration plan, rollback plan, human approval before production deployment |
| Emergency | Production crash, data-corruption risk, critical security issue, server-startup failure | Immediate containment plan, minimal safe fix, security and reliability review, human approval, post-implementation council audit, follow-up permanent architecture proposal |

## Discussion Protocol

```
Solution Architect → Initial Proposal
Technical Reviewer → Challenge
Security, Reliability, and Performance Reviewer → Audit risk
Architect → Response and Updated Proposal
Moderator → Evaluate evidence and consensus
Consensus achieved → Implementation Engineer
Tests and audit → Validation
Human approval → Controlled deployment
```

## Multi-Round Review

A proposal may require multiple rounds until all blocking objections are resolved, remaining risks are explicitly accepted, evidence is sufficient, the proposal is rejected, or the proposal is escalated to a human administrator. Disagreement does not need to disappear, but it must be documented.

## Decision Requirements

Approval requires evidence for:

- **Architecture:** Ownership model, runtime boundaries, lifecycle, APIs, failure states, integration points
- **Security:** Permission validation, input validation, secret handling, safe external API usage, auditability, threat assessment
- **Performance:** CPU cost, memory cost, server-tick impact, load limits, benchmark method, capacity assumptions
- **Reliability:** Retry behavior, timeout behavior, restart recovery, idempotency, partial-failure behavior, cleanup guarantees, corruption prevention
- **Compatibility:** Existing BattleLuck systems, V Rising server behavior, ECS constraints, BepInEx or plugin compatibility, other-mod interactions, configuration migration
- **Maintainability:** Module boundaries, testing strategy, logging, documentation, extension points, dependency ownership
- **Delivery:** Implementation plan, migration plan, deployment plan, rollback plan, acceptance criteria, release gating

If any mandatory category fails, the proposal returns for revision.

## Evidence Standards

Acceptable evidence:
- Current BattleLuck source code
- Official API documentation
- Official framework documentation
- Reproducible tests
- Runtime logs
- Benchmarks
- Profiling results
- Static-analysis reports
- Test-server observations
- Compatibility matrices
- Source-linked implementation references
- Versioned configuration schemas
- CI artifacts

Unacceptable evidence:
- Unsupported assumptions
- Unverified AI claims
- Outdated memory of an API
- Jira status without implementation proof
- Documentation without code verification
- Single successful manual test
- "It should work"
- "No error was thrown"

Priority order:
1. Current BattleLuck source code
2. Official API or framework documentation
3. Reproducible tests and runtime logs
4. Benchmarks and profiling results
5. Static-analysis reports
6. Test-server observations
7. Versioned configuration schemas
8. CI artifacts

## Consensus Model

Consensus requires:
- No unresolved critical objection
- No unresolved security blocker
- No unresolved data-corruption risk
- No unresolved production-stability risk
- Architecture accepted by Technical Reviewer
- Safety accepted by Security and Performance Reviewer
- Evidence accepted by Moderator
- Implementation scope clearly defined

Possible final decisions: Approved, Approved With Conditions, Approved for Prototype Only, Approved for Test Environment, Rejected, Escalated to Human Review.

Only Approved or Approved With Conditions may proceed to implementation. Production deployment always requires human authorization.

## Continuous Improvement Loop

```
Scan BattleLuck Repository
    ↓
Inspect Code, Tests, Docs, Jira, Logs, and Builds
    ↓
Identify Candidate Improvement
    ↓
Solution Architect Creates Proposal
    ↓
Technical Reviewer Challenges Design
    ↓
Security and Performance Reviewer Audits Risk
    ↓
Council Moderator Evaluates Evidence
    ↓
Consensus achieved? → Implementation Engineer → Build, Test, Benchmark → Documentation Updated → Post-Implementation Audit → Human Deployment Approval → Rescan Entire Project
    ↓
Revise or Reject
```

The council does not continuously modify production. It continuously evaluates the project and produces controlled, reviewable changes.

## Post-Implementation Audit

After implementation, the Technical Reviewer and Security and Performance Reviewer must compare the result against the approved proposal. The audit verifies:
- Architecture was implemented as approved
- No undocumented behavior was added
- No security controls were bypassed
- No performance assumptions were violated
- Tests cover approved acceptance criteria
- Documentation matches implementation
- Migration behavior is correct
- Rollback behavior is available
- Configuration defaults are correct
- No unrelated systems were modified

Audit outcomes: Accepted, Accepted With Follow-Up, Implementation Deviation, Regression Detected, Rollback Recommended, Human Review Required.

## BattleLuck Integration

```
Git Repository
    ├── Source Code
    ├── Tests
    ├── Documentation
    └── Generated References
            ↓
    AI Engineering Council
            ↓
    Jira ←→ CI
            ↓
    Decision Records ← Build Evidence
            ↓
    Gemini Documentation Review
            ↓
    Human Approval Gate
            ↓
    Controlled Deployment
```

## Jira Workflow

Recommended statuses: Candidate → Architecture Draft → Technical Review → Security and Performance Review → Revision Required or Council Approval → Implementation → Validation → Post-Implementation Audit → Ready for Human Approval → Done.

Required fields: Proposal ID, Council cycle, Change classification, Affected systems, Architecture document, Reviewer findings, Security findings, Performance findings, Decision status, Approval conditions, Implementation commit, Test evidence, Benchmark evidence, Documentation paths, Migration plan, Rollback plan, Human approver, Deployment result.

## Council Artifacts

Each proposal should produce a versioned folder:

```
docs/council/proposals/BL-AIC-001/
├── proposal.md
├── architecture-review.md
├── security-review.md
├── performance-review.md
├── moderator-decision.md
├── implementation-plan.md
├── implementation-report.md
├── test-evidence.md
├── benchmark-results.md
├── rollback-plan.md
└── post-implementation-audit.md
```

A summary index tracks all proposals in `docs/council/COUNCIL_REGISTER.md`.

## Agent Memory and Context Rules

Each agent must receive the same authoritative project context: current repository state, relevant documentation, Jira issue data, configuration schemas, action catalog, test results, build metadata, runtime logs, previous council decisions, and known limitations.

When project evidence conflicts:
1. Source Code outranks generated documentation
2. Verified runtime test outranks assumed behavior
3. Official API documentation outranks unverified secondary sources
4. Current build outranks old Jira status

## Deadlock and Disagreement Resolution

1. Moderator identifies exact disputed claim
2. Council defines evidence that would resolve it
3. Architect performs additional research or creates prototype
4. Reviewers reassess new evidence
5. Moderator records final outcome

If disagreement remains:
- Low Risk → Document dissent and approve conditionally
- Medium Risk → Require prototype or test-environment validation
- High Risk → Escalate to human review
- Critical Risk → Reject until resolved

## Failure Handling

If an agent fails, times out, or returns incomplete work:
- The proposal remains unapproved
- Missing stage marked incomplete
- Implementation does not begin
- Moderator requests retry or human review
- Existing production behavior remains unchanged

If Implementation Engineer fails midway:
- Partial changes remain isolated in a branch
- No deployment occurs
- Build and test status recorded
- Proposal returns to implementation or architecture review

## Human Governance

Human approval is mandatory for:
- Production deployments
- Server restarts
- Data migrations
- Save-file changes
- Security permission changes
- External service credentials
- Destructive operations
- Major architectural changes
- Changes affecting player progression
- Changes affecting native world data
- Changes affecting session restoration
- Emergency production fixes

The human administrator may approve, reject, pause, request further evidence, restrict scope, require manual testing, or override a non-security recommendation. Critical safety objections should not be overridden without documented acceptance of risk.

## Core Principles

- No unilateral authority
- Evidence before approval
- Security before convenience
- Stability before speed
- Architecture before implementation
- Validation before deployment
- Human control over production
- Complete auditability
- Explicit ownership
- Reversible changes
- Documented disagreement
- Deterministic workflows
- Safe failure behavior
- Continuous improvement without uncontrolled modification

## Final Operating Principle

```
Architect proposes.
Technical Reviewer challenges.
Security and Performance Reviewer protects production.
Moderator verifies consensus and evidence.
Implementation Engineer builds only what was approved.
Tests verify the implementation.
Documentation records the truth.
Humans control production.
```

The council's purpose is not to generate the largest possible number of changes. Its purpose is to produce fewer, safer, better-supported, and more maintainable changes that continuously improve BattleLuck without compromising the live V Rising server, persistent world state, player data, or architectural integrity.