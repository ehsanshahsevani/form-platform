# ADR-013: Authenticated Tenant Resolver

- Status: Accepted

## Context

In Milestone 01, tenant resolution is available only in Development through
an HTTP header. In other environments, `TenantContext` intentionally remains
unresolved and form endpoints reject requests without a tenant. This decision
prevented tenant identity from being forged through an untrusted request value,
but it created active technical debt.

## Problem

There is no valid way to resolve a tenant in non-Development environments.
Production form endpoints are therefore unusable, and cross-tenant integration
tests cannot exercise a real authenticated tenant flow.

## Decision

Define an independent `ITenantResolver` contract that obtains a tenant from an
authenticated source, never from the request body or an untrusted header.
Header-based resolution is permitted only in Development and must be limited by
`IHostEnvironment.IsDevelopment()`. Resolution must validate the user's
membership in the tenant.

## Alternatives

- Allow header-based resolution in every environment: rejected because headers
  can be forged.
- Put the tenant ID in the URL: rejected because it has the same forgery risk
  and can expose tenant IDs in logs.
- Resolve the tenant from the request body: rejected because the architecture
  explicitly prohibits it.

## Consequences

This records formal, trackable technical debt. Identity and TenantManagement
must implement `ITenantResolver`. Integration tests must cover successful and
failed resolution, including cross-tenant access.
