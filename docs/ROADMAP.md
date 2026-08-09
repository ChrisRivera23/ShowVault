# Roadmap

## Current: Prototype readiness

- Pause sequential integration-catalog expansion while installability and recovery proof are hardened.
- Make the prototype installable without repository access or a developer toolchain.
- Validate Install → Enroll → Scan → Backup → Verify → Restore → Prove on personal or otherwise controlled equipment.
- Keep all packaging, defaults, fixtures, and acceptance gates venue-neutral; LIV nightclub is the intended first deployment, not a pilot or design assumption.
- Use [`PROTOTYPE_READINESS.md`](PROTOTYPE_READINESS.md) as the readiness gate and implementation order.

## Next

1. Reproducible macOS operator-application release artifact and clean personal-equipment installation
2. Production-style macOS Agent clean-install, restart, reboot, and upgrade validation
3. Deployable versioned control-plane environment and migration procedure
4. Unified venue-neutral onboarding and preflight
5. Automated success, restart, failure, and tamper readiness matrix
6. Windows packaging and equivalent readiness validation
7. Resume prioritized integration work from observed prototype needs

## Version 1 integration program

The complete launch scope and delivery waves are maintained in [`INTEGRATION_CATALOG.md`](INTEGRATION_CATALOG.md). Resolume portable-bundle/user-data and grandMA2/grandMA3 export recovery are implemented. Yamaha console settings, DME7, MTX/MRX, PC-D/DI, and ProVisionaire Control PLUS/Kiosk protection are implemented. DME5/DME3 project and Custom Control Panel companion protection is in progress. After this Yamaha milestone, Q-SYS is the next representative Version 1 platform foundation. Resolume, Yamaha, and MA Lighting remain the highest priorities.

Version 1 readiness requires an explicit support record for every catalog entry. A manufacturer record names tested product families and versions; a protocol record names the supported capability and conformance boundary. Empty plugins and generic reachability do not count as integration coverage.

## Deferred

Backup Box hardware appliance, remote appliance management, AI recommendations, internationalization expansion, and the plugin catalog beyond the initial MVP set.
