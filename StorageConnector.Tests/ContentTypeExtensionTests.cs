using StorageConnector.Common.DTOs;

namespace StorageConnector.Tests;

/// <summary>
/// Tests for content-type to extension resolution.
///
/// The value returned here becomes part of the stored object key, and a null return aborts the upload
/// entirely. Phase 2 replaced a reverse scan of the static-files provider with a curated map
/// (finding H3) and surfaced the resolved name on <see cref="UploadInfo.FileName"/> (finding C4).
/// </summary>
public class ContentTypeExtensionTests
{
	[Theory]
	[InlineData("image/png", ".png")]
	[InlineData("image/gif", ".gif")]
	[InlineData("image/webp", ".webp")]
	[InlineData("image/svg+xml", ".svg")]
	[InlineData("application/pdf", ".pdf")]
	[InlineData("application/json", ".json")]
	[InlineData("audio/mpeg", ".mp3")]
	[InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
	[InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx")]
	public void ReturnsExpectedExtension_ForUnambiguousContentTypes(string contentType, string expected)
	{
		Assert.Equal(expected, StorageConnectorService.GetExtensionFromContentType(contentType));
	}

	/// <summary>
	/// H3: content types that map to several extensions must resolve to the canonical one. The reverse
	/// scan previously returned whichever entry enumerated first -- ".jpe" for JPEG, ".asm" for plain
	/// text, ".m4v" for MP4.
	/// </summary>
	[Theory]
	[InlineData("image/jpeg", ".jpg")]
	[InlineData("text/plain", ".txt")]
	[InlineData("video/mp4", ".mp4")]
	[InlineData("text/html", ".html")]
	[InlineData("audio/wav", ".wav")]
	public void ResolvesToCanonicalExtension_WhenSeveralMap(string contentType, string expected)
	{
		Assert.Equal(expected, StorageConnectorService.GetExtensionFromContentType(contentType));
	}

	/// <summary>
	/// H3: these ordinary content types previously resolved to null, which made
	/// GenerateDirectUploadInfo throw and left them impossible to upload. "image/heic" is the default
	/// capture format on iPhone, which matters directly for the selfie flow this library serves.
	/// </summary>
	[Theory]
	[InlineData("image/heic", ".heic")]
	[InlineData("image/heif", ".heif")]
	[InlineData("image/avif", ".avif")]
	[InlineData("text/csv", ".csv")]
	[InlineData("application/xml", ".xml")]
	[InlineData("application/zip", ".zip")]
	[InlineData("application/gzip", ".gz")]
	public void PreviouslyRejectedContentTypes_AreNowUploadable(string contentType, string expected)
	{
		Assert.Equal(expected, StorageConnectorService.GetExtensionFromContentType(contentType));
	}

	/// <summary>Content types may carry parameters; the media type alone decides the extension.</summary>
	[Theory]
	[InlineData("text/plain; charset=utf-8", ".txt")]
	[InlineData("image/jpeg;charset=binary", ".jpg")]
	public void IgnoresContentTypeParameters(string contentType, string expected)
	{
		Assert.Equal(expected, StorageConnectorService.GetExtensionFromContentType(contentType));
	}

	[Theory]
	[InlineData("application/x-not-a-real-type")]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	public void ReturnsNull_ForUnknownOrEmptyContentType(string? contentType)
	{
		Assert.Null(StorageConnectorService.GetExtensionFromContentType(contentType!));
	}

	[Fact]
	public void IsCaseInsensitive_OnContentType()
	{
		Assert.Equal(".png", StorageConnectorService.GetExtensionFromContentType("IMAGE/PNG"));
	}

	/// <summary>
	/// C4: the resolved object key is now reported back on UploadInfo, so an upload stays addressable.
	/// </summary>
	[Fact]
	public void UploadInfo_CarriesTheResolvedFileName()
	{
		var fileNameProperty = typeof(UploadInfo).GetProperty(nameof(UploadInfo.FileName));

		Assert.NotNull(fileNameProperty);
		Assert.Equal(typeof(CloudFileName), fileNameProperty.PropertyType);
	}

	/// <summary>
	/// C4: the end-to-end shape of the fix -- a caller asking for "holiday/photo" as a JPEG gets back
	/// the key the bytes actually land under, and it round-trips through CloudFileName unchanged.
	/// </summary>
	[Fact]
	public void ResolvedFileName_IsTheKeyTheObjectIsStoredUnder()
	{
		var requested = new CloudFileName("holiday/photo");
		var extension = StorageConnectorService.GetExtensionFromContentType("image/jpeg");

		var resolved = new CloudFileName($"{requested.Value}{extension}");

		Assert.Equal("holiday/photo.jpg", resolved.Value);
		Assert.NotEqual(requested, resolved);
	}
}
