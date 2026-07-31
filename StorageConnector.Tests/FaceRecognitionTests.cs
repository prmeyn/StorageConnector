using System.Globalization;
using System.Text;
using EarthCountriesInfo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StorageConnector.Common;
using StorageConnector.Common.DTOs;
using StorageConnector.Services.AWS;

namespace StorageConnector.Tests;

/// <summary>
/// Tests for the face recognition split (finding H5).
///
/// Argument validation happens before any network call, so these run without cloud credentials.
/// </summary>
public class FaceRecognitionTests
{
	private static AmazonS3BucketService AwsService()
	{
		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("""
			{
			  "StorageConnectors": {
			    "AWS": {
			      "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" },
			      "Accounts": [ { "BucketName": "my-bucket", "AwsRegion": "eu-west-1" } ]
			    }
			  }
			}
			""")))
			.Build();

		return new AmazonS3BucketService(
			new AmazonS3BucketsInitializer(config), NullLogger<AmazonS3BucketService>.Instance);
	}

	/// <summary>
	/// H5: AWS Rekognition restricts ExternalImageId to [a-zA-Z0-9_.\-:]+. The old code passed the
	/// caller's arbitrary string straight through, so an email address or a name with a space was
	/// rejected by the service at runtime with an opaque error. It is now caught up front, on every
	/// provider, so a subject id means the same thing whichever cloud is configured.
	/// </summary>
	[Theory]
	[InlineData("user@example.com")]   // '@' is not permitted
	[InlineData("Anna Jensen")]        // spaces are not permitted
	[InlineData("bruger/1")]           // '/' is not permitted
	[InlineData("")]
	[InlineData("   ")]
	public async Task RegisterFace_RejectsUnusableSubjectIds(string subjectId)
	{
		var service = AwsService();

		await Assert.ThrowsAsync<ArgumentException>(() =>
			service.RegisterFaceAsync("faces", CountryIsoCode.DK, new CloudFileName("selfie.jpg"), subjectId, TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData("user-123")]
	[InlineData("a.b.c")]
	[InlineData("tenant:42:user_7")]
	[InlineData("0123456789")]
	public void SubjectIdValidation_AcceptsPortableIdentifiers(string subjectId)
	{
		Assert.Equal(subjectId, FaceSubjectId.Validate(subjectId, nameof(subjectId)));
	}

	[Fact]
	public void SubjectIdValidation_RejectsOverlongIdentifiers()
	{
		var tooLong = new string('a', FaceSubjectId.MaxLength + 1);

		var ex = Assert.Throws<ArgumentException>(() => FaceSubjectId.Validate(tooLong, "subjectId"));
		Assert.Contains(FaceSubjectId.MaxLength.ToString(CultureInfo.InvariantCulture), ex.Message);
	}

	/// <summary>
	/// The erasure path must validate its inputs too -- an erasure request that silently does nothing
	/// because of a malformed id would be worse than one that fails.
	/// </summary>
	[Theory]
	[InlineData("user@example.com")]
	[InlineData("")]
	public async Task DeleteRegisteredFaces_RejectsUnusableSubjectIds(string subjectId)
	{
		var service = AwsService();

		await Assert.ThrowsAsync<ArgumentException>(() =>
			service.DeleteRegisteredFacesAsync("faces", CountryIsoCode.DK, subjectId, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task FaceOperations_RequireACollectionName()
	{
		var service = AwsService();

		await Assert.ThrowsAsync<ArgumentException>(() =>
			service.DeleteRegisteredFacesAsync("", CountryIsoCode.DK, "user-123", TestContext.Current.CancellationToken));

		await Assert.ThrowsAsync<ArgumentException>(() =>
			service.RegisterFaceAsync("  ", CountryIsoCode.DK, new CloudFileName("selfie.jpg"), "user-123", TestContext.Current.CancellationToken));
	}

	/// <summary>
	/// H5: reading and writing are now distinct operations. Counting faces must not be reachable
	/// through anything that stores data, and storing must be its own explicit call.
	/// </summary>
	[Fact]
	public void ReadOperations_AreDistinctFromTheWriteOperation()
	{
		var faceInterface = typeof(IFaceRecognitionProvider);

		// Exactly one method stores a biometric template. DeleteRegisteredFacesAsync also mentions
		// "Register", but it removes data rather than writing it, so match on the prefix.
		var storingMethods = faceInterface.GetMethods()
			.Where(m => m.Name.StartsWith("Register", StringComparison.Ordinal))
			.ToList();

		Assert.Single(storingMethods);
		Assert.Equal(nameof(IFaceRecognitionProvider.RegisterFaceAsync), storingMethods[0].Name);

		// Counting takes no collection name at all, so it cannot touch stored data even by accident.
		var countParameters = faceInterface.GetMethod(nameof(IFaceRecognitionProvider.CountFacesAsync))!
			.GetParameters()
			.Select(p => p.Name)
			.ToList();

		Assert.DoesNotContain("faceCollectionName", countParameters);
		Assert.DoesNotContain("subjectId", countParameters);
	}

	/// <summary>
	/// The erasure path must resolve its recognition client from the country, exactly as registration
	/// does. Taking the first configured bucket instead would query whichever Rekognition region
	/// happened to be listed first: a template registered against the Danish account would not be
	/// found, and the call would return 0 and report a successful erasure that never happened.
	///
	/// Guarded structurally, since proving the region routing end to end needs a live AWS account.
	/// </summary>
	[Fact]
	public void DeleteRegisteredFaces_TakesTheCountry_SoItErasesFromTheRightRegion()
	{
		var deleteParameters = typeof(IFaceRecognitionProvider)
			.GetMethod(nameof(IFaceRecognitionProvider.DeleteRegisteredFacesAsync))!
			.GetParameters();

		Assert.Contains(deleteParameters, p => p.ParameterType == typeof(CountryIsoCode));

		// Registration and erasure must agree on how the account is chosen, or they can disagree about
		// where a template lives.
		var registerParameters = typeof(IFaceRecognitionProvider)
			.GetMethod(nameof(IFaceRecognitionProvider.RegisterFaceAsync))!
			.GetParameters();

		Assert.Equal(
			registerParameters.Count(p => p.ParameterType == typeof(CountryIsoCode)),
			deleteParameters.Count(p => p.ParameterType == typeof(CountryIsoCode)));
	}

	/// <summary>
	/// M9: quality thresholds were hard-coded constants. Defaults must match the previous values so
	/// existing deployments behave identically.
	/// </summary>
	[Fact]
	public void FaceQualityDefaults_MatchThePreviousHardCodedValues()
	{
		var defaults = new FaceQualitySettings();

		Assert.Equal(50.0f, defaults.MinSharpness);
		Assert.Equal(30.0f, defaults.MinBrightness);
		Assert.Equal(80.0f, defaults.MinConfidence);
		Assert.Equal(98.0f, defaults.MatchThreshold);
	}

	[Fact]
	public void FaceQuality_IsConfigurable()
	{
		var config = new ConfigurationBuilder()
			.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes("""
			{
			  "StorageConnectors": {
			    "AWS": {
			      "AwsCredentials": { "AccessKey": "AKIAEXAMPLE", "SecretAccessKey": "secret" },
			      "FaceQuality": { "MinSharpness": 10.5, "MatchThreshold": 90 },
			      "Accounts": [ { "BucketName": "my-bucket", "AwsRegion": "eu-west-1" } ]
			    }
			  }
			}
			""")))
			.Build();

		var initializer = new AmazonS3BucketsInitializer(config);

		Assert.Equal(10.5f, initializer.FaceQuality.MinSharpness);
		Assert.Equal(90.0f, initializer.FaceQuality.MatchThreshold);
		Assert.Equal(30.0f, initializer.FaceQuality.MinBrightness); // unspecified, so default
	}
}
