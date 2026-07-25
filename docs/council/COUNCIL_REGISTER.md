# AI Engineering Council Register

This register tracks all council proposals, their status, and key artifacts.

## Proposal Index

| ID | Title | Classification | Status | Architect Review | Technical Review | Security Review | Moderator Decision | Implementation | Human Deployment Approval | Jira Issue |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BL-AIC-001 | *(Pending first proposal)* | Standard | draft | - | - | - | - | - | - | - |

## Status Values

- `draft` — Proposal is being drafted
- `architecture_draft` — Architecture document in progress
- `technical_review` — Under technical review
- `security_review` — Under security and performance review
- `revision_required` — Returned to architect for changes
- `council_approval` — Approved for implementation
- `implementation` — Implementation in progress
- `validation` — Testing and validation
- `post_implementation_audit` — Post-implementation review
- `ready_for_human_approval` — Awaiting human deployment authorization
- `done` — Deployed and closed
- `rejected` — Proposal rejected
- `escalated` — Escalated to human administrator

## Review Outcomes

### Technical Reviewer
- `approved`
- `approved_with_conditions`
- `revision_required`
- `rejected`
- `insufficient_evidence`

### Security and Performance Reviewer
- `approved`
- `approved_with_mitigations`
- `revision_required`
- `rejected`
- `production_unsafe`

### Moderator
- `return_to_architect`
- `request_additional_research`
- `request_prototype`
- `request_benchmark`
- `request_security_mitigation`
- `request_compatibility_review`
- `approve_for_implementation`
- `reject_proposal`
- `escalate_to_human`

## Change Classifications

- `minor` — Documentation, typo, non-functional refactor, test naming
- `standard` — New command, action, config option, UI behavior, runtime validation
- `major` — Snapshot schema, persistence model, AI execution flow, networking, player data
- `emergency` — Production crash, data-corruption risk, critical security, server startup failure

## Evidence Sources (Priority Order)

1. Current BattleLuck source code
2. Official API / framework documentation
3. Reproducible tests and runtime logs
4. Benchmarks and profiling results
5. Static-analysis reports
6. Test-server observations
7. Versioned configuration schemas
8. CI artifacts

Lower-priority sources (do not override higher-priority evidence):
- Unsupported assumptions
- Unverified AI claims
- Outdated API memory
- Jira status without implementation proof
- Single manual test
- "It should work"

## Governance Reminders

- No agent may deploy to production without explicit human authorization.
- Implementation engineer may not deviate from approved architecture.
- All production deployments require human approval.
- Emergency changes still require post-implementation council audit.
- Disagreement must be documented, not suppressed.