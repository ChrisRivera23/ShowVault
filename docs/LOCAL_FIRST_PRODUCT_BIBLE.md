# ShowVault Pro local-first product directive

This document is the repository-level authority for ShowVault's architecture and delivery order. It records the Product Owner's 2026-08-10 directive from `SHOWVAULT_DEPENDENCY_PROTECTION_README.md`. If an older handoff or prototype document conflicts with this direction, this document wins.

## Product promise

ShowVault Pro protects the complete working environment required to recover a live-production system:

**Install → initialize local vault → Scan → review → Save or Cancel → collect dependencies → verify locally → retain offline → synchronize verified copies → restore**

AWS is a later delivery environment, not a prerequisite for proving the product. Development must first prove the complete local workflow with a local ASP.NET Core backend, PostgreSQL, Flutter desktop client, automated tests, Docker, and an object-store substitute.

## Non-negotiable local-first behavior

- Internet loss must not prevent scan, review, local backup, manifest creation, local verification, restore preparation, or access to an existing verified recovery point.
- The local vault is the authoritative staging and offline-recovery source.
- A successful Save creates a new immutable recovery point and never overwrites the previous known-good point.
- Local protection and cloud synchronization are separate states.
- Only verified recovery points enter the durable cloud-upload queue.
- Uploads must eventually be resumable, idempotent, checksum-verified, and safe across restarts and connectivity loss.
- A failed cloud operation must never delete the only local verified copy.
- PostgreSQL stores catalog and workflow metadata. Backup payloads belong in the local vault and, later, object storage. The database must not be the only copy of a manifest.

The default configurable vault is:

```text
Documents/
└── ShowVault Pro/
    ├── Backups/
    ├── Manifests/
    ├── Device Exports/
    ├── Upload Queue/
    ├── Reports/
    ├── Logs/
    └── Quarantine/
```

## Dependency-aware recovery

A parent project or device configuration is not a complete backup by itself. The dependency graph is a first-class domain model covering required files, companion software, plugins, presets, mappings, databases, media, fonts, codecs, runtimes, drivers, firmware, licenses and reacquisition evidence, networks, routes, clocks, control relationships, exact versions, locations, compatibility constraints, recoverability states, and verification evidence.

Discovery must identify those assets and relationships. Backup must capture or document them. Verification must prove integrity, graph closure, and compatibility. Restore must reconstruct them in dependency order. The Recovery Confidence Score must fall when required dependencies are missing or unverified.

No integration may be called fully supported until its versioned knowledge pack defines detection, exact supported locations or vendor export procedures, required and optional assets, exclusions, licensing and secret handling, version compatibility, verification, restore ordering, and representative tests.

## Safety and support levels

A scan is bounded, permission-aware, rate-limited, and read-only. It must never modify a live console, application, route, or device merely to identify it. Every integration declares one support level: Automatic, Assisted, Inventory only, or Unsupported/unverified.

ShowVault must not blindly copy installed-application directories, caches, logs, installers, binaries, temporary files, or guessed locations. It must not imply that a backup transfers a software license. Secrets and certificates require a separate protected workflow.

## Minimum local proof before AWS

The local build must initialize the vault, detect a supported desktop application and simulated or controlled device, show findings and changes, implement Save and Cancel, collect parent files and known dependencies, create and verify an immutable recovery point and manifest, queue it while offline, synchronize it to a local object-store substitute after reconnection, restore into a clean controlled target, and produce an explainable Recovery Confidence Score.

Recommended first ecosystems are Resolume Arena/Avenue and DiGiCo/Dante or Yamaha/Dante. Synthetic fixtures remain the default; personal or venue equipment requires explicit authorization.
