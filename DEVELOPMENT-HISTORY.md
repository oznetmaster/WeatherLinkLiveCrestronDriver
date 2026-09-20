# Development and validation history

See the [product changelog](CHANGELOG.md) for shipped changes. This document preserves test, CI and build history. Dated development entries describe work at that time, not a published product version or completed acceptance. Version headings identify the release alongside which development work was recorded; processor-test versions identify separate test packages.

## Where changes belong

- Product changelog and product release notes: shipped behavior, API, compatibility, fixes and runtime dependencies. Mention validation briefly when it helps explain a fix.
- This history: test coverage, CI, build tooling, test-package releases and work on pending candidates. Split mixed entries so the product effect remains easy to find.
- Testing and workflow guides: current setup and operating instructions.
- Test-only or documentation-only changes do not require a product release. Processor-test releases update this history, not the product changelog.

<!-- development-history -->

## Weather display and package validation - 2026-09-20 (pending driver release)

- Add regression coverage for local pressure and cloud pressure, wind and rainfall units. Prepare corrections for a driver patch release.
- Preserve the embedded driver definition when naming the output assembly explicitly; preserve the existing driver identity.
- Include product help and merged-dependency notices in ordinary builds, and normalize archive entry names.
- Use ManifestUtil 29.0.10 for driver releases and test-host SDK 1.12.1. Release validation compares discovered test identities instead of a duplicated test-count constant.

## Offline release workflow option - 2026-09-15 (no package release)

- Allow an explicit manual release when local hardware or the self-hosted runner is unavailable, with the reason and exact source recorded in the workflow summary.
- Keep hosted source validation mandatory and preserve all build, test and packaging steps. No runtime, API or package-version changes.

## CI package cleanup - 2026-09-15 (no driver or processor package release)

- Update Test Explorer workflow containers to CrestronHomeNUnit.TestAdapter 1.3.0 and document opt-in storage cleanup after successful CI runs.
- Retain original deployment filenames, protect pre-existing/manual packages and preserve failed-run evidence. Cleanup frees archive storage without rebooting; Home can retain cached catalogue entries until its next planned reboot.
- Compare executed test identities and packaged discovery against source discovery instead of duplicated count constants. Live suites remain discovery-only in hosted CI; only the documented processor-runtime skips are accepted on Windows.
- Actual driver/library code is unchanged; no driver release is required.

## CI validation - 2026-09-15 (no package release)

- Revalidate the current default-branch source after successful release workflows, including version commits created by GitHub Actions.
- Allow maintainers to configure exact-source, App-specific checks that must pass before publishing through `RELEASE_REQUIRED_CHECKS`; missing, failed or unconfirmed checks block the release.

## WeatherLinkLiveCrestronDriver.ProcessorTests v1.1.1 - 2026-09-15

Published processor test package on GitHub. This is a test-package release only; no driver or library NuGet package is published. See the matching package release notes for changes and validation.

## 2026-09-15 - Test and development tooling (no driver release)

- Add the published Test Explorer workflow adapter, offline discovery CI and independent GitHub processor-test releases. Private workflow plans control optional live tests, actual-driver updates and temporary-instance cleanup.

- Add three optional live driver tests for measured station readings, repeated refresh and unit selection. The processor package now contains 59 tests. Private station settings are supplied as test inputs and are never packaged.

- Clarify that automatic forecast coordinates come from the native processor location, not Home's separate setting. Document the native console command, SDK reboot requirement, and paired driver override alternative. Documentation only; no driver behavior change.

## 2.0.13 — 2026-09-14

- Cover local readings, failure cache retention, cloud refresh throttling and delayed local/cloud responses after configuration changes or disposal. Reject stale responses before they can restore cached weather or online state.

- Normalize the working manifest from `2.0.011.0005` to `2.0.012.0005`; this aligns the development version family with the latest existing three-part release. No historical tags or packages are changed.

- Standardize driver versioning: Debug project/package metadata follows the manifest including its build increment; local Release builds preserve it; three-part release tags select the exact CI release without another patch increment. Verify source and built package versions before publication.

- Expand driver coverage to 40 offline tests and 16 SDK entity/lifecycle tests, with a desktop SDK harness and the same lifecycle fixtures in the net472 processor package.

- Reject NaN latitude and longitude overrides. Add an internal location provider for deterministic desktop entity testing; the production constructor still reads the processor location.

## 2.0.12 - 2026-09-13

- Add 40 NUnit driver unit tests and a processor lifecycle suite in the existing solution.

- Add a standalone Utility processor test package with private Debug deployment settings.

### Validation

- 40 offline NUnit tests pass on Windows; the actual packaged unit suite also passes twice in one process.

- The processor lifecycle test is available for hardware validation and is skipped on Windows.