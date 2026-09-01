# ADR-0001: Product Identity And Compatibility

- Status: Accepted for implementation; product-owner approval pending
- Date: 2026-09-01

## Decision

Use `GOStructAnalysis` for product-facing UI, reports, documentation, and product metadata. Retain
`TrussAnalyzer.sln`, `TrussAnalyzer.*` assemblies/namespaces, legacy APIs, and current file schemas until
a separately tested migration is approved.

## Consequences

The product can establish one direction without breaking consumers. Source identifiers will differ
temporarily from the displayed product name. Bulk rename is explicitly deferred and must not be hidden
inside a UI rewrite.
