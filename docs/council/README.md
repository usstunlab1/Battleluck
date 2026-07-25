# AI Engineering Council Documentation

This directory contains the documentation for the BattleLuck Autonomous AI Engineering Council.

## Contents

### Core Documentation

- **COUNCIL_REGISTER.md** — Master tracking document for all council proposals, their status, review outcomes, and key artifacts.
- **proposals/** — Versioned proposal directories. Each proposal follows the structure:

  ```
  docs/council/proposals/BL-AIC-XXX/
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

## Council Workflow

The council follows a structured, multi-stage process:

1. **Proposal** — Solution Architect drafts a proposal.
2. **Technical Review** — Technical Reviewer challenges the design from an engineering quality perspective.
3. **Security & Performance Review** — Security and Performance Reviewer audits for risks.
4. **Moderator Evaluation** — Council Moderator verifies consensus and evidence.
5. **Implementation** — Implementation Engineer builds only the approved specification.
6. **Validation** — Tests and benchmarks verify the implementation.
7. **Human Approval** — Human administrator approves production deployment.
8. **Audit** — Post-implementation audit ensures faithful execution.

No single agent has unilateral authority. All significant changes require council consensus and human deployment approval.

## Change Classifications

- **Minor** — Documentation corrections, typos, non-functional refactors
- **Standard** — New commands, actions, config options, UI behavior
- **Major** — Snapshot schema changes, persistence models, AI execution flow changes, player data
- **Emergency** — Production crashes, data corruption risks, critical security issues

## Integration

The council integrates with the BattleLuck engineering workflow through:

- **Git Repository** — Source code, tests, and documentation versioning
- **Jira** — Issue tracking and status management
- **CI** — Build evidence and test validation
- **Human Approval Gate** — Manual approval for production deployments

## Principles

- No unilateral authority
- Evidence before approval
- Security before convenience
- Stability before speed
- Architecture before implementation
- Validation before deployment
- Human control over production
- Complete auditability
- Reversible changes

See the [BattleLuck Architecture Documentation](../ARCHITECTURE.md) for the system context within which the council operates.