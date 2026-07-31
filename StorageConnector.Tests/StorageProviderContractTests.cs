using System.Reflection;
using StorageConnector.Common;
using StorageConnector.Services.AWS;
using StorageConnector.Services.Azure;
using StorageConnector.Services.GCP;

namespace StorageConnector.Tests;

/// <summary>
/// Guards contracts that the compiler does not enforce and that broke silently before.
/// </summary>
public class StorageProviderContractTests
{
	private static readonly Type[] Implementations =
	[
		typeof(StorageConnectorService),
		typeof(AzureBlobStorageService),
		typeof(AmazonS3BucketService),
		typeof(GCPStorageService),
	];

	// GCP has no face support and deliberately does not implement IFaceRecognitionProvider.
	private static readonly Type[] FaceImplementations =
	[
		typeof(StorageConnectorService),
		typeof(AzureBlobStorageService),
		typeof(AmazonS3BucketService),
	];

	/// <summary>
	/// H1: C# binds optional-argument defaults at the CALL SITE, so an implementation declaring a
	/// different default than its interface silently changes behaviour depending on whether the caller
	/// holds the interface or the concrete type. StorageConnectorService used to declare 1 where
	/// IStorageProvider declared 60, meaning an upload URL expired in one minute or sixty depending
	/// only on the caller's variable type. Nothing in the compiler catches this.
	/// </summary>
	[Theory]
	[InlineData(nameof(IStorageProvider.GenerateDirectUploadInfo))]
	[InlineData(nameof(IStorageProvider.GenerateDirectDownloadInfo))]
	public void ExpiryDefault_IsIdenticalAcrossInterfaceAndEveryImplementation(string methodName)
	{
		var expected = ExpiryDefaultOf(typeof(IStorageProvider), methodName);

		Assert.Equal(IStorageProvider.DefaultExpiryInMinutes, expected);

		foreach (var implementation in Implementations)
		{
			Assert.Equal(expected, ExpiryDefaultOf(implementation, methodName));
		}
	}

	/// <summary>
	/// M1: HasAccounts only reads an in-memory dictionary built at start-up. As
	/// <c>async Task&lt;bool&gt;</c> with no await it allocated a state machine per call, and the
	/// aggregate service invoked it up to six times per request.
	/// </summary>
	[Fact]
	public void HasAccounts_IsASynchronousProperty_OnEveryImplementation()
	{
		Assert.NotNull(typeof(IStorageProvider).GetProperty(nameof(IStorageProvider.HasAccounts)));

		foreach (var implementation in Implementations)
		{
			var property = implementation.GetProperty(nameof(IStorageProvider.HasAccounts));

			Assert.NotNull(property);
			Assert.Equal(typeof(bool), property.PropertyType);
			Assert.Null(implementation.GetMethod(nameof(IStorageProvider.HasAccounts)));
		}
	}

	/// <summary>
	/// M2: every async entry point must accept a CancellationToken, and it must be optional so the
	/// addition stays source-compatible for callers who do not pass one.
	/// </summary>
	[Theory]
	[InlineData(nameof(IStorageProvider.GenerateDirectUploadInfo))]
	[InlineData(nameof(IStorageProvider.GenerateDirectDownloadInfo))]
	public void AsyncMethods_AcceptAnOptionalCancellationToken(string methodName)
	{
		foreach (var type in Implementations.Append(typeof(IStorageProvider)))
		{
			AssertTrailingOptionalCancellationToken(type, methodName);
		}
	}

	[Theory]
	[InlineData(nameof(IFaceRecognitionProvider.CountFacesAsync))]
	[InlineData(nameof(IFaceRecognitionProvider.FindMatchingFacesAsync))]
	[InlineData(nameof(IFaceRecognitionProvider.RegisterFaceAsync))]
	[InlineData(nameof(IFaceRecognitionProvider.DeleteRegisteredFacesAsync))]
	public void FaceMethods_AcceptAnOptionalCancellationToken(string methodName)
	{
		foreach (var type in FaceImplementations.Append(typeof(IFaceRecognitionProvider)))
		{
			AssertTrailingOptionalCancellationToken(type, methodName);
		}
	}

	private static void AssertTrailingOptionalCancellationToken(Type type, string methodName)
	{
		var method = type.GetMethod(methodName)!;
		var parameter = method.GetParameters().SingleOrDefault(p => p.ParameterType == typeof(CancellationToken));

		Assert.True(parameter is not null, $"{type.Name}.{methodName} takes no CancellationToken.");
		Assert.True(parameter.HasDefaultValue, $"{type.Name}.{methodName} CancellationToken is not optional.");
		Assert.Equal(method.GetParameters().Length - 1, parameter.Position);
	}

	/// <summary>
	/// H5: storage and face recognition are separate interfaces now. Bundling them is what allowed a
	/// read-shaped call to quietly store biometric data, and it forced GCP -- which has no face support
	/// at all -- to carry throwing stubs.
	/// </summary>
	[Fact]
	public void FaceRecognition_IsNotPartOfTheStorageInterface()
	{
		Assert.Null(typeof(IStorageProvider).GetMethod("GetFaceInfo"));
		Assert.DoesNotContain(typeof(IStorageProvider).GetMethods(), m => m.Name.Contains("Face", StringComparison.Ordinal));

		Assert.False(typeof(GCPStorageService).IsAssignableTo(typeof(IFaceRecognitionProvider)));
	}

	/// <summary>
	/// H5: exactly one method may store biometric data, and there must be a way to erase it again.
	/// </summary>
	[Fact]
	public void FaceInterface_SeparatesReadsFromWrites_AndOffersErasure()
	{
		var readOnly = new[]
		{
			nameof(IFaceRecognitionProvider.CountFacesAsync),
			nameof(IFaceRecognitionProvider.FindMatchingFacesAsync),
		};

		foreach (var name in readOnly)
		{
			Assert.NotNull(typeof(IFaceRecognitionProvider).GetMethod(name));
		}

		Assert.NotNull(typeof(IFaceRecognitionProvider).GetMethod(nameof(IFaceRecognitionProvider.RegisterFaceAsync)));
		Assert.NotNull(typeof(IFaceRecognitionProvider).GetMethod(nameof(IFaceRecognitionProvider.DeleteRegisteredFacesAsync)));
	}

	private static int ExpiryDefaultOf(Type type, string methodName)
	{
		var parameter = type.GetMethod(methodName)!
			.GetParameters()
			.Single(p => p.Name == "expiryInMinutes");

		Assert.True(parameter.HasDefaultValue, $"{type.Name}.{methodName} has no default for expiryInMinutes.");
		return (int)parameter.DefaultValue!;
	}

	/// <summary>
	/// C7: Azure declared a nullable return against a non-nullable interface member and returned null
	/// on failure, so callers received a NullReferenceException. No face method may return a nullable
	/// result -- failures propagate as exceptions instead.
	/// </summary>
	[Theory]
	[InlineData(nameof(IFaceRecognitionProvider.CountFacesAsync))]
	[InlineData(nameof(IFaceRecognitionProvider.FindMatchingFacesAsync))]
	[InlineData(nameof(IFaceRecognitionProvider.RegisterFaceAsync))]
	public void FaceMethods_ReturnNonNullableResults(string methodName)
	{
		var expected = typeof(IFaceRecognitionProvider).GetMethod(methodName)!.ReturnType;

		foreach (var implementation in FaceImplementations)
		{
			var method = implementation.GetMethod(methodName)!;
			Assert.Equal(expected, method.ReturnType);

			var nullability = new NullabilityInfoContext().Create(method.ReturnParameter);
			Assert.Equal(NullabilityState.NotNull, nullability.GenericTypeArguments[0].ReadState);
		}
	}
}
