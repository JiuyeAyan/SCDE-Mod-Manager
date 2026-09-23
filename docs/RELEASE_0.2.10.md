# Manager 0.2.10 source update

Published source scope: Manager, built-in component source/prepared resources, tests, notices and build documentation. This update does not publish a new EXE Release, change repository visibility, close issues, or include optional gameplay Mods.

## Since the previous 0.2.8 source snapshot

- Create/prepare the managed game copy only when launching, not during Manager startup; reuse current copies. A one-time migration of older isolation layouts remains a launch-time safety check.
- Build with the latest official SE candidate after validation; this snapshot supplies SE 2.8.0+scdemm.1 instead of the old fixed 2.6.0 package.
- MMC 0.4.0 uses actual runtime identities for protocol v4, supports standalone installation, and explicitly warns when SE-only peers have unverified extra plugins.
- Reserve built-in runtime paths, isolate dirty original installations, preserve declared/known persistent Mod data, and retain recoverable backups on failed deployment.
- Validate SE candidate APIs/dependencies and game-patch shapes before relying on them.
- Document package schema/dependency ranges and offer changed same-version imported archives for explicit confirmation.

See [issue #1 implementation notes](GITHUB_ISSUE_1_FIXES_0.2.9.md), [0.2.10 startup/build details](MANAGER_0.2.10_BUNDLED_SE_STARTUP.md), and [SE integration](SE_INTEGRATION.md). Historical implementation notes describe the validation available at their original dates; their statements that no upload had yet occurred are historical, not this publication's status.

## Verification boundary

The curated source snapshot passed all 148 Node tests on Windows with Node.js 24.19.0. Three tests inspecting separate Fog/Advanced Control sources are excluded, as in the original publication. No game assemblies, crash dumps, personal logs or unrelated source projects are uploaded. This publication does not claim a new game session, two-PC multiplayer test, third-party crash fix, or future-version compatibility guarantee.
