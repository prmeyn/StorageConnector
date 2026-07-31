# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build -c Release                      # solution
dotnet test  -c Release                      # all tests (134)
dotnet test  -c Release --filter "FullyQualifiedName~CloudFileNameTests"                    # one class
dotnet test  -c Release --filter "FullyQualifiedName~GcpV4SignerTests.SignatureIsLowercaseHex"  # one test
dotnet pack StorageConnector/StorageConnector.csproj -c Release --output ./artifacts        # .nupkg + .snupkg
```

Use `--no-incremental` when checking warning counts; an incremental build reports zero because nothing
recompiled.

**The build must stay at 0 warnings.** `.editorconfig` deliberately raises culture-sensitivity
(CA1304/CA1305/CA1310/CA1862), structured-logging (CA2254) and async-without-await (CS1998) to
warnings, because each of those masked a real defect here. `GenerateDocumentationFile` is on, so
missing `<param>` tags also surface as warnings.

## Architecture

### One provider is active per deployment

The name says multi-cloud, but only one cloud is ever used at a time. `StorageConnectorService`
resolves `ActiveProvider` in the order **AWS → Azure → GCP**, taking the first with usable accounts,
and forwards every call to it. The `CountryIsoCodeMapToAccountName` map selects an account *within*
that provider — it does not route between clouds. This is intentional; do not "fix" it into
cross-cloud routing.

### Initializer / Service split

Each provider is two singletons:

- **`*Initializer`** — reads `IConfiguration` once at start-up, validates it, and constructs the SDK
  clients into a dictionary keyed by account/bucket name.
- **`*Service`** — implements the public interfaces using those pre-built clients.

Two rules this split exists to enforce:

1. **The configuration binder does not enforce `required`.** It leaves properties null. Every setting
   must be validated explicitly in the initializer and failures thrown as
   `StorageConnectorConfigurationException`, quoting the **full configuration path**
   (`StorageConnectors:AWS:Accounts:0:AwsRegion`). Without this the failure surfaces from deep inside
   a cloud SDK naming nothing useful.
2. **`HasAccounts` reports usable clients, not configuration rows.** An account whose credentials
   could not be resolved must never be advertised as available.

### Two interfaces, deliberately separate

| Interface | Implemented by |
|---|---|
| `IStorageProvider` — pre-signed upload/download URLs | all three providers |
| `IFaceRecognitionProvider` — face counting, matching, registration, erasure | AWS, Azure (**not GCP**) |

They are apart because storing a biometric template is a fundamentally different act from generating
an upload URL. Bundling them once meant a read-shaped call silently wrote GDPR Article 9
special-category data. Preserve these properties when changing that interface:

- `CountFacesAsync` and `FindMatchingFacesAsync` **only read**. `CountFacesAsync` deliberately takes no
  collection name so it cannot touch stored data.
- `RegisterFaceAsync` is the **only** method that stores a template.
- `DeleteRegisteredFacesAsync` exists so erasure requests can be honoured, and **takes the country** —
  templates live in the recognition service of the account that country maps to, so registration and
  erasure must resolve their account identically. Getting this wrong makes deletions silently no-op
  while reporting success.
- `GCPStorageService` does not implement the interface at all, rather than carrying throwing stubs.

### Provider capability matrix

Not everything works everywhere. Unsupported operations throw `NotSupportedException` naming the
provider — never return an empty result, which previously made Azure report "no matches" for everyone.

| | AWS | Azure | GCP |
|---|:--:|:--:|:--:|
| Upload URL | ✅ | ✅ | ✅ |
| Download URL | ✅ | — | — |
| Count / register / erase faces | ✅ | ✅ | — |
| Match faces | ✅ | — | — |

### Types with non-obvious contracts

- **`CloudFileName`** (struct) — `default(CloudFileName)` bypasses the constructor and all validation,
  so **`.Value` throws** on an uninitialised instance while `.ToString()` stays safe for debuggers and
  logs. Library code must read `.Value`. The constructor lowercases with `ToLowerInvariant`; the
  culture-sensitive overload produced different object keys under `tr-TR`. Its `JsonConverter` throws
  rather than yielding `default`, so deserialization isn't a back door around validation.
- **`UploadInfo.FileName`** — the key the object is *actually* stored under. The service appends an
  extension derived from the content type, so it may differ from what the caller asked for. Callers
  must persist it or their later download URL won't resolve.
- **`ContentTypeExtensions`** — a curated MIME→extension map is the authority, built once. Do not
  revert to reverse-scanning `FileExtensionContentTypeProvider`: that table is many-to-one and
  incomplete, so `image/jpeg` resolved to `.jpe`, `text/plain` to `.asm`, and HEIC/CSV/XML/ZIP to
  `null`, which made those types impossible to upload.
- **`AccountSelector`** — the single country→account fallback rule shared by all providers. Each once
  had its own, so one config typo failed three different ways.
- **`GcpV4Signer`** — Google Cloud Storage V4 signing, implemented directly rather than pulling in
  `Google.Cloud.Storage.V1`. The signing step is an injected `SignAsync` delegate so the canonical
  request is unit-testable; production signs via IAM `SignBlob`, so no private key is ever held.

## Testing

xunit **v3** (`OutputType` must be `Exe`). Tests reach `internal` members via `InternalsVisibleTo`.
No cloud credentials are needed — argument and configuration validation runs before any network call,
and the V4 signer's signing step is injectable.

- Pass `TestContext.Current.CancellationToken` to any method that accepts one (v3 analyzer `xUnit1051`).
- Assertions that pin **known-wrong** behaviour are marked `CHARACTERIZATION (<finding id>)` and are
  *expected to fail when the corresponding fix lands* — that failure is the signal, not a regression.
- `ProviderStartupTests` builds each provider from a JSON config snippet; it is the guard against
  start-up crashes and against the README's own examples drifting out of sync with the code.

## Conventions and constraints

- **Tabs**, block-scoped namespaces (`namespace X { }`) — matches the existing code; `.editorconfig`
  deliberately does not prescribe a namespace style.
- **`net10.0` only.** Multi-targeting `net8.0` fails because `EarthCountriesInfo` targets net10 only,
  and .NET 8 support ends November 2026, so it isn't worth pursuing.
- Currently a **2.0.0 breaking release**; source-breaking changes are acceptable and should be batched.
- Long-lived cloud keys are the only supported auth. Managed Identity / IAM roles / Workload Identity
  Federation are a documented gap, not an oversight.

## CI/release

`ci.yml` runs build + test + pack on push and PR. `release.yml` fires on a `v*.*.*` tag, **runs the
tests before publishing**, and authenticates to NuGet via **Trusted Publishing** — a GitHub OIDC token
exchanged for a one-hour key, so no `NUGET_API_KEY` secret exists. All actions are **pinned to commit
SHAs**; if you change one, fetch the SHA from the GitHub API rather than writing it from memory.
`dotnet nuget push` uploads the adjacent `.snupkg` automatically — don't add a separate symbols step.

Known release-time issue: NU5104, because the stable package depends on the prerelease
`Azure.AI.Vision.Face`. Unresolved by design pending a decision.
