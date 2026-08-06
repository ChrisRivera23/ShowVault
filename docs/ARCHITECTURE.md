# Architecture

## System shape

- Flutter client using feature-first organization, Riverpod, and GoRouter.
- ASP.NET Core 9 API using Clean Architecture and constructor injection.
- PostgreSQL through Entity Framework Core migrations.
- Versioned REST endpoints with OpenAPI, Problem Details, correlation IDs, and asynchronous operations.
- Local storage abstraction with cloud-ready adapters.

## Platform model

The Digital Twin maintains one operational graph combining physical, network, software, dependency, and recovery topology. Views are derived from this graph.

Universal objects support create, read, versioned update, archive, compare, validate, clone, export, audit, and relate. Standard relationships include contains, controls, uses, depends on, connected to, synchronizes with, manages, generates, and requires.

## Recovery execution fabric

The public contract exposes business operations: start restore, generate recovery plan, and production readiness. Internal execution is coordinated by restore workers, checkpoint management, validation, assets, and plugins.

Recovery chain of trust: verified identity → authorization → approval → verified backup → verified recovery plan → trusted plugins → secure execution → post-restore validation → automatic verification.

## Service rules

- Jobs call services; services call APIs; APIs access data through the data layer.
- Services communicate through versioned contracts, never assumptions.
- Create operations accept optional idempotency keys.
- Responses include status, message, correlation ID, version, timestamp, and payload.
- Long-running operations expose progress and cancellation.
