# StorageConnector

[![NuGet](https://img.shields.io/nuget/v/StorageConnector.svg)](https://www.nuget.org/packages/StorageConnector)
[![NuGet Downloads](https://img.shields.io/nuget/dt/StorageConnector.svg)](https://www.nuget.org/packages/StorageConnector)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

**A unified interface for multi-cloud storage operations** - StorageConnector is an open-source C# library that provides a consistent abstraction layer for cloud storage services, enabling seamless integration with Azure Blob Storage, AWS S3, and Google Cloud Storage.

---

## ✨ Features

- 🌐 **Multi-Cloud Support** - Azure Blob Storage, AWS S3, and Google Cloud Storage
- 🔐 **Pre-Signed URLs** - Generate secure direct upload/download URLs for client-side operations
- 🌍 **Geographic Routing** - Route storage operations based on country ISO codes for data residency compliance
- 🤖 **AI Integration** - Built-in facial recognition support (Azure Face API, AWS Rekognition)
- 💉 **Dependency Injection** - First-class support for ASP.NET Core DI
- 🎯 **Type Safety** - Strongly-typed configuration and DTOs
- 📦 **Easy Configuration** - Simple JSON-based configuration

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
builder.Services.AddStorageConnector(builder.Configuration);
```

### 2. Configuration

Add the following to your `appsettings.json`:

> **⚠️ Security Warning:** Never commit real credentials to source control. Use environment variables, Azure Key Vault, AWS Secrets Manager, or similar secure storage for production credentials.

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
        "EU": "your-s3-bucket"
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
    public async Task<IActionResult> GenerateUploadUrl([FromBody] UploadRequest request)
    {
        // Generate a pre-signed upload URL for client-side upload
        var uploadInfo = await _storageConnectorService.GenerateDirectUploadInfo(
            countryOfResidenceIsoCode: CountryIsoCode.US,
            fileReferenceWithPath: new CloudFileName($"uploads/{Guid.NewGuid()}"),
            contentType: "image/png",
            expiryInMinutes: 15
        );

        return Ok(uploadInfo);
    }
}
```

**Response Model:**

```csharp
public sealed record UploadInfo
{
    [JsonPropertyName("directUploadUrl")]
    public required string DirectUploadUrl { get; init; }
    
    [JsonPropertyName("method")]
    public required string HttpMethod { get; init; }

    [JsonPropertyName("headers")]
    public required Dictionary<string, string> Headers { get; init; }
}
```

---

## 📖 Key Concepts

### Country-Based Routing

StorageConnector can route files to different storage accounts based on country ISO codes, helping you comply with data residency requirements (GDPR, etc.):

```csharp
// Files from EU users go to EU storage
var uploadInfo = await _storageConnectorService.GenerateDirectUploadInfo(
    CountryIsoCode.DE, // Germany
    new CloudFileName("user-data/profile.jpg"),
    "image/jpeg"
);
```

### Direct Upload/Download

Generate pre-signed URLs to allow clients to upload/download directly to/from cloud storage without routing through your server:

```csharp
// Generate upload URL (client uploads directly to cloud)
var uploadInfo = await _storageConnectorService.GenerateDirectUploadInfo(...);

// Generate download URL (client downloads directly from cloud)
var downloadInfo = await _storageConnectorService.GenerateDirectDownloadInfo(...);
```

### Face Recognition Integration

StorageConnector includes built-in support for facial recognition:

```csharp
var faceInfo = await _storageConnectorService.GetFaceInfo(
    faceListName: "user-faces",
    regionCountryIsoCode: CountryIsoCode.US,
    fileNameWithExtension: new CloudFileName("faces/user123.jpg"),
    userData: "user-metadata"
);
```

---

## 🏗️ Architecture

StorageConnector uses a **provider pattern** with a unified interface (`IStorageProvider`) implemented by:

- `AzureBlobStorageService` - Azure Blob Storage operations
- `AmazonS3BucketService` - AWS S3 operations  
- `GCPStorageService` - Google Cloud Storage operations

The main `StorageConnectorService` orchestrates between providers based on configuration and country routing.

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

