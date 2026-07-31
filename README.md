# StorageConnector

[![NuGet](https://img.shields.io/nuget/v/StorageConnector.svg)](https://www.nuget.org/packages/StorageConnector)
[![NuGet Downloads](https://img.shields.io/nuget/dt/StorageConnector.svg)](https://www.nuget.org/packages/StorageConnector)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

**Let your users upload files straight to cloud storage — without the bytes ever passing through your
server.** 🚀

StorageConnector is an open-source C# library that generates short-lived, pre-signed upload and
download URLs for Azure Blob Storage, AWS S3 and Google Cloud Storage, behind a single friendly API.
Your app hands the URL to the browser or mobile client, and the client sends the file straight to the
cloud.

Every one of those clouds already supports this. Each of them does it differently. StorageConnector
gives you **one way to ask**.

### ⚡ How it works

```
  Browser / mobile app              Your API                     Cloud storage
          │                            │                               │
          │  1. "I want to upload"     │                               │
          │ ─────────────────────────► │                               │
          │                            │ 2. GenerateDirectUploadInfo(…)│
          │  3. URL + method + headers │                               │
          │ ◄───────────────────────── │                               │
          │                                                            │
          │  4. PUT the file bytes directly to the signed URL          │
          │ ─────────────────────────────────────────────────────────► │
```

Your server never sees the file — so no upload bandwidth through your app, no memory pressure from
buffering, and no request timeouts on large files. 🎉

### ✨ Features

- 🔐 **Pre-Signed URLs** - Short-lived, secure upload and download URLs for direct client-side transfers
- 🌐 **Cloud Portability** - Write your upload logic once and switch clouds with a config change.
  *One provider is active per deployment* — this is about moving between clouds, not using several
  at the same time
- 🌍 **Country-Based Account Routing** - Choose which storage account or bucket a file lands in, based
  on the user's ISO country code
- 🤖 **Face Recognition** - Built for selfie and identity-check flows: count faces in an uploaded
  image, match against people you registered earlier, and erase someone's biometric data on request
- 💉 **Dependency Injection** - First-class support for ASP.NET Core DI
- 🎯 **Type Safety** - Strongly-typed configuration and DTOs, with `CancellationToken` throughout
- 📦 **Easy Configuration** - Simple JSON-based configuration

### 🚧 What it doesn't do

StorageConnector is a pre-signed URL generator, not a general-purpose storage abstraction. There's
**no** API for listing, deleting, copying or moving objects, reading or writing metadata, or streaming
file content through your server. Need those? Reach for your cloud provider's own SDK — it works
happily alongside this library on the same account. 👍

> **📄 Licence heads-up:** StorageConnector is GPLv3. That's a strong copyleft licence and it affects
> what you can build on top of it, so do check it suits your project before adopting — see
> [License](#-license).

---

## 📦 Installation

### NuGet Package

Install via .NET CLI:

```bash
dotnet add package StorageConnector
```

Or via Package Manager Console:

```powershell
Install-Package StorageConnector
```

**Package Links:**
- 📦 [NuGet Gallery](https://www.nuget.org/packages/StorageConnector)
- 💻 [GitHub Repository](https://github.com/prmeyn/StorageConnector)

---

## 🚀 Quick Start

### 1. Configure Services

Add StorageConnector to your `Program.cs` or `Startup.cs`:

```csharp
builder.Services.AddStorageConnectors();
```

Configuration is read from the `StorageConnectors` section of the application's `IConfiguration`, so
no argument is needed. The call returns the `IServiceCollection`, so it chains.

### 2. Configuration

Add the following to your `appsettings.json`:

> **⚠️ Security Warning:** Never commit real credentials to source control. Use environment variables, Azure Key Vault, AWS Secrets Manager, or similar secure storage for production credentials.
>
> StorageConnector currently authenticates with **long-lived keys only** (Azure account keys, AWS
> access keys, a GCP service account key). It does not yet support Managed Identity, IAM roles for
> service accounts, or Workload Identity Federation — any of which would remove these secrets from
> configuration entirely. If you are deploying into Azure, EKS or GKE, treat that as a known gap.

> **ℹ️ Country codes:** keys in `CountryIsoCodeMapToAccountName` must be ISO 3166-1 alpha-2 **country** codes (`DK`, `DE`, `US`, `IN`). Region groupings such as `EU` are not country codes and will fail at start-up.

```json
{
  "StorageConnectors": {
    "Azure": {
      "CountryIsoCodeMapToAccountName": {
        "US": "yourstorageaccount"
      },
      "Accounts": [
        {
          "AccountName": "yourstorageaccount",
          "AccountKey": "YOUR_AZURE_STORAGE_ACCOUNT_KEY",
          "ContainerName": "your-container-name"
        }
      ]
    },
    "AWS": {
      "AwsCredentials": {
        "AccessKey": "YOUR_AWS_ACCESS_KEY",
        "SecretAccessKey": "YOUR_AWS_SECRET_KEY"
      },
      "CountryIsoCodeMapToAccountName": {
        "DE": "your-s3-bucket"
      },
      "Accounts": [
        {
          "BucketName": "your-s3-bucket",
          "AwsRegion": "eu-west-1",
          "AwsCredentials": {
            "AccessKey": "YOUR_AWS_ACCESS_KEY",
            "SecretAccessKey": "YOUR_AWS_SECRET_KEY"
          }
        }
      ]
    },
    "GCP": {
      "GcpCredentials": {
        "type": "service_account",
        "project_id": "your-project-id",
        "private_key_id": "YOUR_PRIVATE_KEY_ID",
        "private_key": "-----BEGIN PRIVATE KEY-----\nYOUR_PRIVATE_KEY\n-----END PRIVATE KEY-----\n",
        "client_email": "your-service-account@your-project.iam.gserviceaccount.com",
        "client_id": "YOUR_CLIENT_ID",
        "auth_uri": "https://accounts.google.com/o/oauth2/auth",
        "token_uri": "https://oauth2.googleapis.com/token",
        "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
        "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/your-service-account%40your-project.iam.gserviceaccount.com",
        "universe_domain": "googleapis.com"
      },
      "CountryIsoCodeMapToAccountName": {
        "IN": "your-gcp-bucket"
      },
      "Accounts": [
        {
          "BucketName": "your-gcp-bucket",
          "ServiceAccountEmail": "your-service-account@your-project.iam.gserviceaccount.com"
        }
      ]
    }
  }
}
```

### 3. Usage Example

Inject `StorageConnectorService` into your classes:

```csharp
using StorageConnector;
using EarthCountriesInfo;

public class FileUploadController : ControllerBase
{
    private readonly StorageConnectorService _storageConnectorService;

    public FileUploadController(StorageConnectorService storageConnectorService)
    {
        _storageConnectorService = storageConnectorService;
    }

    [HttpPost("generate-upload-url")]
    public async Task<IActionResult> GenerateUploadUrl(
        [FromBody] UploadRequest request,
        CancellationToken cancellationToken)
    {
        // Generate a pre-signed upload URL for client-side upload
        var uploadInfo = await _storageConnectorService.GenerateDirectUploadInfo(
            countryOfResidenceIsoCode: CountryIsoCode.US,
            fileReferenceWithPath: new CloudFileName($"uploads/{Guid.NewGuid()}"),
            contentType: "image/png",
            expiryInMinutes: 15,
            cancellationToken: cancellationToken
        );

        // uploadInfo.FileName is the key the object is stored under - save it, you will
        // need it to generate a download URL later.
        return Ok(uploadInfo);
    }
}
```

**Response Model:**

```csharp
public sealed record UploadInfo
{
    // The object key the file is actually stored under. The service appends the extension
    // derived from the content type, so this may differ from the name you requested.
    // Persist it - it is the handle you pass to GenerateDirectDownloadInfo later.
    [JsonPropertyName("fileName")]
    public required CloudFileName FileName { get; init; }

    [JsonPropertyName("directUploadUrl")]
    public required string DirectUploadUrl { get; init; }

    [JsonPropertyName("method")]
    public required string HttpMethod { get; init; }

    [JsonPropertyName("headers")]
    public required Dictionary<string, string> Headers { get; init; }
}
```

> **⚠️ Upgrading from 1.x:** `UploadInfo.FileName` is new. Requesting an upload for `photo` with
> content type `image/jpeg` stores the object as `photo.jpg`, so save `FileName` alongside your own
> records — without it the download URL you generate later will not resolve.

### 4. Upload from the client

`UploadInfo` tells the client everything it needs: where to send the file, which HTTP method to use,
and which headers to set. Send the bytes exactly as described — the headers are part of what was
signed, so changing them will make the cloud reject the request.

```javascript
// uploadInfo is the JSON your endpoint returned
const response = await fetch(uploadInfo.directUploadUrl, {
  method: uploadInfo.method,          // "PUT"
  headers: uploadInfo.headers,        // e.g. { "Content-Type": "image/png" }
  body: file,                         // the File / Blob straight from an <input type="file">
});

if (!response.ok) {
  throw new Error(`Upload failed: ${response.status}`);
}

// Send uploadInfo.fileName back to your API and store it - it is how you
// reference this object from now on.
```

The file goes from the user's device to the cloud. It never touches your API.

---

## 📖 Key Concepts

### Country-Based Account Routing

StorageConnector routes files to different **accounts or buckets within a single configured cloud
provider**, based on the user's country ISO code:

```csharp
// A German user's file goes to whichever account "DE" maps to in your configuration
var uploadInfo = await _storageConnectorService.GenerateDirectUploadInfo(
    CountryIsoCode.DE, // Germany
    new CloudFileName("user-data/profile.jpg"),
    "image/jpeg",
    cancellationToken: cancellationToken
);
```

> **⚠️ Scope of this feature.** One cloud provider is active per deployment — if AWS is configured,
> Azure and GCP are not consulted. Routing selects an account *within* that provider; it does not
> route between clouds.
>
> Placing data in a given region can be **one input** to a data-residency strategy, but this library
> does not by itself make a deployment GDPR-compliant. Where the bucket physically lives, what your
> cloud provider's terms say, sub-processor arrangements, and transfer mechanisms are all outside its
> control. Keys in `CountryIsoCodeMapToAccountName` are ISO 3166-1 alpha-2 **country** codes
> (`DK`, `DE`, `US`) — region groupings such as `EU` are not valid and will fail at start-up.

### Downloading

Downloads work the same way in reverse: you generate a short-lived URL and the client fetches the
file straight from the cloud.

```csharp
var downloadInfo = await _storageConnectorService.GenerateDirectDownloadInfo(
    countryOfResidenceIsoCode: CountryIsoCode.DK,
    fileReferenceWithPath: storedFileName,   // the UploadInfo.FileName you saved earlier
    expiryInMinutes: 15,
    cancellationToken: cancellationToken);

return Ok(new { url = downloadInfo.DirectDownloadUrl });
```

Pass the **`FileName` returned by the upload**, not the name you originally requested — the service
appends an extension derived from the content type, so the two can differ.

> **Provider support:** download URLs are currently implemented for **AWS only**. Azure and GCP throw
> `NotSupportedException`. Uploads work on all three.

### URL lifetimes

`expiryInMinutes` defaults to 60 and controls how long a generated URL stays valid. Keep it as short
as your client flow allows — anyone holding the URL can use it until it expires.

Signed URLs are backdated by five minutes so a client whose clock runs slightly behind your server is
not rejected. Google Cloud Storage caps a signed URL at seven days total, including that allowance.

### Face Recognition

> **⚠️ Biometric data.** Face templates are special-category personal data under GDPR Article 9.
> Only `RegisterFaceAsync` stores anything; make sure you have a lawful basis before calling it, and
> wire `DeleteRegisteredFacesAsync` into your erasure process.

Reading and writing are separate calls, so nothing is stored unless you ask for it:

```csharp
// READ - counts good-quality faces. Stores nothing.
int faces = await _storageConnectorService.CountFacesAsync(
    regionCountryIsoCode: CountryIsoCode.DK,
    fileNameWithExtension: new CloudFileName("faces/user123.jpg"),
    cancellationToken);

// READ - who does this look like? Stores nothing, and does not add the queried face.
IReadOnlySet<string> matches = await _storageConnectorService.FindMatchingFacesAsync(
    faceCollectionName: "user-faces",
    regionCountryIsoCode: CountryIsoCode.DK,
    fileNameWithExtension: new CloudFileName("faces/user123.jpg"),
    cancellationToken);

// WRITE - stores a biometric template. Deliberate, and erasable.
RegisteredFace registered = await _storageConnectorService.RegisterFaceAsync(
    faceCollectionName: "user-faces",
    regionCountryIsoCode: CountryIsoCode.DK,
    fileNameWithExtension: new CloudFileName("faces/user123.jpg"),
    subjectId: "user-123",
    cancellationToken);

// ERASE - honour a deletion request. Returns how many templates were removed.
int deleted = await _storageConnectorService.DeleteRegisteredFacesAsync(
    faceCollectionName: "user-faces",
    regionCountryIsoCode: CountryIsoCode.DK,   // must match the country used to register
    subjectId: "user-123",
    cancellationToken);
```

> **⚠️ Erasure and country codes.** Templates live in the recognition service belonging to the account
> the country maps to. Pass the **same** `regionCountryIsoCode` you registered with — erasing with a
> different country searches a different collection, finds nothing, and returns `0` as though the
> deletion succeeded. If you register faces, store the country alongside the subject id.

`subjectId` is your own identifier for the person and is restricted to letters, digits and `_ . - :`
(max 255 characters) — the character set AWS Rekognition permits. Use a user id or a hashed
reference rather than an email address or a display name.

**Provider support:**

| Operation | AWS | Azure | GCP |
|---|:--:|:--:|:--:|
| `CountFacesAsync` | ✅ | ✅ | — |
| `FindMatchingFacesAsync` | ✅ | — | — |
| `RegisterFaceAsync` | ✅ | ✅ | — |
| `DeleteRegisteredFacesAsync` | ✅ | ✅ | — |

Azure requires a `VisionAccount` section (see configuration above). Check
`SupportsFaceRecognition` before calling, or handle `NotSupportedException`.

Detection thresholds are configurable under `StorageConnectors:AWS:FaceQuality`
(`MinSharpness`, `MinBrightness`, `MinConfidence`, `MatchThreshold`).

---

## 🏗️ Architecture

Inject **`StorageConnectorService`** and you can ignore everything below it — it picks the configured
provider and forwards the call.

It implements two interfaces, deliberately kept apart:

| Interface | Purpose |
|---|---|
| `IStorageProvider` | Pre-signed upload and download URLs |
| `IFaceRecognitionProvider` | Face counting, matching, registration and erasure |

They are separate because storing a biometric template is a very different act from generating an
upload URL, and bundling them once meant a read-shaped call was quietly writing special-category
personal data. `GCPStorageService` implements only the first — it has no face support at all, rather
than stubs that throw.

Behind the facade, one provider is active per deployment:

- `AmazonS3BucketService` — AWS S3 and Rekognition
- `AzureBlobStorageService` — Azure Blob Storage and Face API
- `GCPStorageService` — Google Cloud Storage (V4 signed URLs, signed via IAM so no private key is held)

Selection order is AWS → Azure → GCP: the first with usable credentials wins. Within that provider,
the country map chooses the account.

---

## 🤝 Contributing

We welcome contributions! Here's how you can help:

1. 🐛 **Report bugs** - [Open an issue](https://github.com/prmeyn/StorageConnector/issues)
2. 💡 **Suggest features** - [Start a discussion](https://github.com/prmeyn/StorageConnector/discussions)
3. 🔧 **Submit PRs** - Fork, create a feature branch, and submit a pull request

### Development Setup

```bash
git clone https://github.com/prmeyn/StorageConnector.git
cd StorageConnector
dotnet restore
dotnet build
```

---

## 📋 Requirements

- **.NET 10.0** or later
- Cloud provider accounts (Azure, AWS, and/or GCP)

---

## 📄 License

This project is licensed under the **GNU General Public License v3.0** - see the [LICENSE](LICENSE) file for details.

---

## 🔗 Links

- 📦 [NuGet Package](https://www.nuget.org/packages/StorageConnector)
- 💻 [GitHub Repository](https://github.com/prmeyn/StorageConnector)
- 📖 [Documentation](https://github.com/prmeyn/StorageConnector#readme)
- 🐛 [Issue Tracker](https://github.com/prmeyn/StorageConnector/issues)

---

## 🙏 Acknowledgments

Built with ❤️ using:
- [Azure.Storage.Blobs](https://www.nuget.org/packages/Azure.Storage.Blobs)
- [AWSSDK.S3](https://www.nuget.org/packages/AWSSDK.S3)
- [Google.Cloud.Iam.Credentials.V1](https://www.nuget.org/packages/Google.Cloud.Iam.Credentials.V1)
- [EarthCountriesInfo](https://www.nuget.org/packages/EarthCountriesInfo)

---

**Happy coding!** 🚀🌐📚

