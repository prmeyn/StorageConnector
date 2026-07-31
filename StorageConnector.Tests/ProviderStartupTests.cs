using System.Text;
using EarthCountriesInfo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using StorageConnector.Services.AWS;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector.Tests;

/// <summary>
/// Startup regression tests. Every provider initializer runs inside the DI container, so anything
/// they throw is an application that will not boot.
///
/// Phase 1 replaced the crash-on-start failure modes (C1, C2, C6, C8, C9) with either a supported
/// configuration or an explicit <see cref="StorageConnectorConfigurationException"/> naming the exact
/// configuration path at fault. These tests assert that contract.
/// </summary>
public class ProviderStartupTests
{
	private static IConfiguration Config(string json) =>
		new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
			.Build();

	private const string AzureAccountsOnly = """
	{
	  "StorageConnectors": {
	    "Azure": {
	      "CountryIsoCodeMapToAccountName": { "DK": "mystorageaccount" },
	      "Accounts": [
	        { "AccountName": "mystorageaccount", "AccountKey": "a2V5", "ContainerName": "uploads" }
	      ]
	    }
	  }
	}
	""";

	// ---------------------------------------------------------------- Azure

	/// <summary>
	/// C1 (fixed): the "VisionAccount" section is optional. Blob storage without the Face API is a
	/// supported setup and must start cleanly -- it previously threw NullReferenceException.
	/// </summary>
	[Fact]
	public void Azure_WithoutVisionAccount_StartsCleanly()
	{
		var initializer = new AzureBlobStoragesInitializer(Config(AzureAccountsOnly));

		Assert.True(new AzureBlobStorageService(initializer, NullLogger<AzureBlobStorageService>.Instance)
			.HasAccounts);
	}

	/// <summary>
	/// C8 (fixed): with no VisionAccount configured the face clients are null. Calling a face API must
	/// report that clearly instead of dereferencing null, and the provider must not claim to support
	/// face recognition in the first place.
	/// </summary>
	[Fact]
	public async Task Azure_WithoutVisionAccount_FaceApiFailsWithActionableMessage()
	{
		var service = new AzureBlobStorageService(
			new AzureBlobStoragesInitializer(Config(AzureAccountsOnly)),
			NullLogger<AzureBlobStorageService>.Instance);

		Assert.False(service.SupportsFaceRecognition);

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			service.CountFacesAsync(CountryIsoCode.DK, new CloudFileName("selfie.jpg"), TestContext.Current.CancellationToken));

		Assert.Contains("VisionAccount", ex.Message);
	}

	[Fact]
	public void Azure_WithVisionAccount_Constructs()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "Azure": {
		      "CountryIsoCodeMapToAccountName": { "DK": "mystorageaccount" },
		      "Accounts": [
		        { "AccountName": "mystorageaccount", "AccountKey": "a2V5", "ContainerName": "uploads" }
		      ],
		      "VisionAccount": {
		        "Endpoint": "https://example.cognitiveservices.azure.com/",
		        "ApiKey": "not-a-real-key"
		      }
		    }
		  }
		}
		""");

		var initializer = new AzureBlobStoragesInitializer(config);
		Assert.True(new AzureBlobStorageService(initializer, NullLogger<AzureBlobStorageService>.Instance)
			.HasAccounts);
	}

	[Fact]
	public void Azure_WithNoSectionAtAll_Constructs_AndReportsNoAccounts()
	{
		var initializer = new AzureBlobStoragesInitializer(Config("{}"));
		Assert.False(new AzureBlobStorageService(initializer, NullLogger<AzureBlobStorageService>.Instance)
			.HasAccounts);
	}

	[Fact]
	public void Azure_WithSectionButNoAccounts_NamesTheMissingPath()
	{
		var config = Config("""{ "StorageConnectors": { "Azure": { "CountryIsoCodeMapToAccountName": {} } } }""");

		var ex = Assert.Throws<StorageConnectorConfigurationException>(() => new AzureBlobStoragesInitializer(config));
		Assert.Contains("StorageConnectors:Azure:Accounts", ex.Message);
	}

	[Fact]
	public void Azure_WithAccountMissingKey_NamesTheMissingPath()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "Azure": {
		      "Accounts": [ { "AccountName": "mystorageaccount", "ContainerName": "uploads" } ]
		    }
		  }
		}
		""");

		var ex = Assert.Throws<StorageConnectorConfigurationException>(() => new AzureBlobStoragesInitializer(config));
		Assert.Contains("StorageConnectors:Azure:Accounts:0:AccountKey", ex.Message);
	}

	// ------------------------------------------------------------------ AWS

	/// <summary>
	/// C2 (fixed): the top-level "AwsCredentials" block is optional when every account supplies its
	/// own. This previously threw NullReferenceException.
	/// </summary>
	[Fact]
	public void Aws_WithoutCommonCredentials_StartsCleanly()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "CountryIsoCodeMapToAccountName": { "DK": "my-bucket" },
		      "Accounts": [
		        {
		          "BucketName": "my-bucket",
		          "AwsRegion": "eu-west-1",
		          "AwsRegionRekognition": "eu-west-1",
		          "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" }
		        }
		      ]
		    }
		  }
		}
		""");

		var initializer = new AmazonS3BucketsInitializer(config);
		Assert.True(new AmazonS3BucketService(initializer, NullLogger<AmazonS3BucketService>.Instance)
			.HasAccounts);
	}

	[Fact]
	public void Aws_WithNoSectionAtAll_Constructs_AndReportsNoAccounts()
	{
		var initializer = new AmazonS3BucketsInitializer(Config("{}"));
		Assert.False(new AmazonS3BucketService(initializer, NullLogger<AmazonS3BucketService>.Instance)
			.HasAccounts);
	}

	/// <summary>
	/// C6 (fixed): a bucket whose credentials cannot be resolved was silently dropped from the client
	/// map while HasAccounts() kept reporting true, so the first real call died with
	/// "Sequence contains no elements". It now fails at startup, naming the bucket and both places
	/// credentials may be supplied.
	/// </summary>
	[Fact]
	public void Aws_WithAccountsButNoCredentials_FailsAtStartup_NamingTheBucket()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "AwsCredentials": { "AccessKey": "", "SecretAccessKey": "" },
		      "CountryIsoCodeMapToAccountName": { "DK": "my-bucket" },
		      "Accounts": [
		        { "BucketName": "my-bucket", "AwsRegion": "eu-west-1", "AwsRegionRekognition": "eu-west-1" }
		      ]
		    }
		  }
		}
		""");

		var ex = Assert.Throws<StorageConnectorConfigurationException>(() => new AmazonS3BucketsInitializer(config));

		Assert.Contains("my-bucket", ex.Message);
		Assert.Contains("AwsCredentials", ex.Message);
	}

	/// <summary>
	/// C9 (fixed): AwsRegionRekognition is optional and defaults to the bucket's own region, so the
	/// README's account block -- which omits it -- now starts. It previously failed inside the AWS SDK
	/// with "Value cannot be null. (Parameter 'key')", naming neither the setting nor the provider.
	/// </summary>
	[Fact]
	public void Aws_WithoutRekognitionRegion_FallsBackToBucketRegion()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" },
		      "CountryIsoCodeMapToAccountName": { "DK": "my-bucket" },
		      "Accounts": [ { "BucketName": "my-bucket", "AwsRegion": "eu-west-1" } ]
		    }
		  }
		}
		""");

		var initializer = new AmazonS3BucketsInitializer(config);
		Assert.True(new AmazonS3BucketService(initializer, NullLogger<AmazonS3BucketService>.Instance)
			.HasAccounts);
	}

	[Fact]
	public void Aws_WithAccountMissingRegion_NamesTheMissingPath()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" },
		      "Accounts": [ { "BucketName": "my-bucket" } ]
		    }
		  }
		}
		""");

		var ex = Assert.Throws<StorageConnectorConfigurationException>(() => new AmazonS3BucketsInitializer(config));
		Assert.Contains("StorageConnectors:AWS:Accounts:0:AwsRegion", ex.Message);
	}

	/// <summary>
	/// H10 (fixed): a country mapped to a bucket that is not configured must fall back to the first
	/// bucket, exactly as Azure already did. AWS previously threw from First(predicate), so the same
	/// configuration typo failed differently depending on which cloud was in use.
	/// </summary>
	[Fact]
	public async Task Aws_WithCountryMappedToUnknownBucket_FallsBackInsteadOfThrowing()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" },
		      "CountryIsoCodeMapToAccountName": { "DK": "bucket-that-does-not-exist" },
		      "Accounts": [ { "BucketName": "real-bucket", "AwsRegion": "eu-west-1" } ]
		    }
		  }
		}
		""");

		var service = new AmazonS3BucketService(
			new AmazonS3BucketsInitializer(config), NullLogger<AmazonS3BucketService>.Instance);

		var uploadInfo = await service.GenerateDirectUploadInfo(
			CountryIsoCode.DK, new CloudFileName("photo.png"), "image/png", cancellationToken: TestContext.Current.CancellationToken);

		Assert.Contains("real-bucket", uploadInfo.DirectUploadUrl);
	}

	// ---------------------------------------------------- README fidelity

	/// <summary>
	/// C3 (fixed): the AWS configuration block exactly as published in README.md, which now uses the
	/// country code "DE" rather than the region grouping "EU". Following the documented quick-start
	/// must start an application. Note the block still omits AwsRegionRekognition, which Phase 1 made
	/// optional -- so this covers C3 and C9 together.
	/// </summary>
	[Fact]
	public void ReadmeAwsExample_StartsCleanly()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "AwsCredentials": {
		        "AccessKey": "YOUR_AWS_ACCESS_KEY",
		        "SecretAccessKey": "YOUR_AWS_SECRET_KEY"
		      },
		      "CountryIsoCodeMapToAccountName": { "DE": "your-s3-bucket" },
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
		    }
		  }
		}
		""");

		var initializer = new AmazonS3BucketsInitializer(config);
		Assert.True(new AmazonS3BucketService(initializer, NullLogger<AmazonS3BucketService>.Instance)
			.HasAccounts);
	}

	/// <summary>
	/// Guards the mistake the README used to document: a region grouping is not a country code, and
	/// the resulting error must say so and name the section it came from.
	/// </summary>
	[Fact]
	public void RegionGroupingAsCountryCode_FailsWithAnExplanatoryMessage()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "AWS": {
		      "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" },
		      "CountryIsoCodeMapToAccountName": { "EU": "your-s3-bucket" },
		      "Accounts": [ { "BucketName": "your-s3-bucket", "AwsRegion": "eu-west-1" } ]
		    }
		  }
		}
		""");

		var ex = Assert.Throws<ArgumentException>(() => new AmazonS3BucketsInitializer(config));

		Assert.Contains("EU", ex.Message);
		Assert.Contains("StorageConnectors:AWS:CountryIsoCodeMapToAccountName", ex.Message);
	}

	// ------------------------------------------------------------------ GCP

	[Fact]
	public void Gcp_WithNoSectionAtAll_Constructs_AndReportsNoAccounts()
	{
		var initializer = new GCPStoragesInitializer(Config("{}"));
		Assert.False(new GCPStorageService(initializer, NullLogger<GCPStorageService>.Instance)
			.HasAccounts);
	}

	/// <summary>
	/// H9: credentials are loaded via CredentialFactory, which replaces the deprecated
	/// GoogleCredential.FromJson. Asking for a ServiceAccountCredential explicitly means a key of the
	/// wrong type is rejected at start-up with an actionable message, rather than failing later at the
	/// first signing call.
	/// </summary>
	[Fact]
	public void Gcp_WithNonServiceAccountCredentials_FailsWithAnActionableMessage()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "GCP": {
		      "Accounts": [
		        { "BucketName": "my-bucket", "ServiceAccountEmail": "sa@project.iam.gserviceaccount.com" }
		      ],
		      "GcpCredentials": {
		        "type": "authorized_user",
		        "client_id": "not-a-service-account",
		        "client_secret": "secret",
		        "refresh_token": "token"
		      }
		    }
		  }
		}
		""");

		var ex = Assert.Throws<StorageConnectorConfigurationException>(() => new GCPStoragesInitializer(config));

		Assert.Contains("StorageConnectors:GCP:GcpCredentials", ex.Message);
		Assert.Contains("service_account", ex.Message);
	}

	[Fact]
	public void Gcp_WithoutCredentials_NamesTheMissingPath()
	{
		var config = Config("""
		{
		  "StorageConnectors": {
		    "GCP": {
		      "Accounts": [
		        { "BucketName": "my-bucket", "ServiceAccountEmail": "sa@project.iam.gserviceaccount.com" }
		      ]
		    }
		  }
		}
		""");

		var ex = Assert.Throws<StorageConnectorConfigurationException>(() => new GCPStoragesInitializer(config));
		Assert.Contains("StorageConnectors:GCP:GcpCredentials", ex.Message);
	}
}
