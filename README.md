# StorageConnector: A Unified Interface for Cloud Storage.
**StorageConnector** is an open-source C# class library that provides a wrapper around existing services that are used to store files in the Cloud
## Features

- Integrates with Azure Blob Storage


## Contributing

We welcome contributions! If you find a bug, have an idea for improvement, please submit an issue or a pull request on GitHub.

## Getting Started

### [NuGet Package](https://www.nuget.org/packages/StorageConnector)

To include **StorageConnector** in your project, [install the NuGet package](https://www.nuget.org/packages/StorageConnector):

```bash
dotnet add package StorageConnector
```
Then in your `appsettings.json` add the following sample configuration and change the values to match the details of your credentials to the various services.
```json
   "StorageConnectors": {
  "GCP": {
    "GcpCredentials": {
      "type": "service_account",
      "project_id": "usignin",
      "client_email": "storage-uploader@usignin.iam.gserviceaccount.com",
      "client_id": "107874315417817060036",
      "auth_uri": "https://accounts.google.com/o/oauth2/auth",
      "token_uri": "https://oauth2.googleapis.com/token",
      "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
      "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/storage-uploader%40usignin.iam.gserviceaccount.com",
      "universe_domain": "googleapis.com",
	   "private_key_id": "941cbf4c78db6fcebaecb8f83751ec3974faf922",
		"private_key": "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDWWF/JYRlR7pON\nl4O2TypUOczZcHozNtiPbSxFZbc2bXXpOf3wCueX8OghVNJE0U15o6AICwKwk2Qp\n3It5rvxpcbUTZOabuhAG84iEXtFJ1MO5HJAoGAf6SMJHfq3K7wD2X8j8zL\nBr8YCzo3Mv7ZT9R1cvd9QN/lclfAqfHaXJbzbj9whHMHKYED9teBPbDj8Po39sqi\njQf3yB2s7cjTaRc6J7XJ79pqtmFQC1odiCusRGRT+Bw/qLPIL6etnuC5ZnmjWbOp\nN+0WUFri3331YT2DjHDgsHc=\n-----END PRIVATE KEY-----\n"
}

    },
    "CountryIsoCodeMapToAccountName": {
      "IN": "globolpayprofilepicseu"
    },
    "Accounts": [
      {
        "BucketName": "globolpayprofilepicseu",
        "ServiceAccountEmail": "storage-uploader@usignin.iam.gserviceaccount.com"
      }
    ]
  },
  "Azure": {
    "CountryIsoCodeMapToAccountName": {
      "IN": "globolpayprofilepicseu"
    },
    "Accounts": [
      {
        "AccountName": "globolpayprofilepicseu",
        "AccountKey": "59fLrSFyFDvGaEL+eJhME55QnnhWqYQz7SeAqLCj+AStQHx8DQ==",
        "ContainerName": "profilepics"
      }
    ]
  },
  "AWS": {
    "AwsCredentials": {
      "AccessKey": "AKIAUQ4L23GGXGCQGLNY",
      "SecretAccessKey": "8r86sD1LGNmbACN5cXr/7o2Cs0"
    },
    "CountryIsoCodeMapToAccountName": {
      "DK": "globolpayprofilepicseu"
    },
    "Accounts": [
      {
        "BucketName": "globolpayprofilepicseu",
        "AwsRegion": "eu-north-1",
        "AwsCredentials": {
          "AccessKey": "AKIAUQ4L23GGXGCQGLNY",
          "SecretAccessKey": "8r86sD1LGNmbACN5cXr/7o2Cs0"
        }
      }
    ]
  }
},
  ```

After the above is done, you can just Dependency inject the `StorageConnector` in your C# class.

#### For example:



```csharp
private readonly StorageConnectorService _storageConnectorService;

...

var uploadInfo = await _storageConnectorService.GenerateDirectUploadInfo("IN", Guid.NewGuid(), contentType: "image/png");

...
// The following info is sent to your client i.e. web browser or app:
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

### GitHub Repository
Visit our GitHub repository for the latest updates, documentation, and community contributions.
https://github.com/prmeyn/StorageConnector


## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE.

Happy coding! 🚀🌐📚


