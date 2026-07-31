using System.Globalization;
using StorageConnector.Common.DTOs;

namespace StorageConnector.Tests;

/// <summary>
/// Characterization tests for <see cref="CloudFileName"/>, the type responsible for the "type safety"
/// guarantee around object keys. Pins current behaviour ahead of the Phase 2 changes (H6, H7, L5-L7).
/// </summary>
public class CloudFileNameTests
{
	[Theory]
	[InlineData("photo.jpg")]
	[InlineData("uploads/2026/photo.jpg")]
	[InlineData("a")]
	[InlineData("folder/sub-folder/file_name.tar.gz")]
	public void Accepts_ReasonableKeys(string name)
	{
		Assert.Equal(name.ToLowerInvariant(), new CloudFileName(name).ToString());
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Rejects_NullOrWhitespace(string? name)
	{
		Assert.Throws<ArgumentException>(() => new CloudFileName(name!));
	}

	[Theory]
	[InlineData("../etc/passwd")]      // path traversal
	[InlineData("./relative")]
	[InlineData("/leading-slash")]
	[InlineData("trailing-slash/")]
	[InlineData("trailing-dot.")]
	[InlineData("double//slash")]
	[InlineData("with\0null")]
	[InlineData("with\nnewline")]
	[InlineData("has<angle>brackets")]
	[InlineData("has:colon")]
	[InlineData("has|pipe")]
	[InlineData("has?question")]
	[InlineData("has*star")]
	public void Rejects_UnsafeOrInvalidKeys(string name)
	{
		Assert.Throws<ArgumentException>(() => new CloudFileName(name));
	}

	[Fact]
	public void Rejects_KeysLongerThan1024Characters()
	{
		Assert.Throws<ArgumentException>(() => new CloudFileName(new string('a', 1025)));
		_ = new CloudFileName(new string('a', 1024)); // boundary is inclusive
	}

	/// <summary>
	/// CHARACTERIZATION (H6): the constructor lowercases the key, so the original casing is lost.
	/// S3 and GCS keys are case-sensitive, so this silently rewrites where the object lands.
	/// </summary>
	[Fact]
	public void LowercasesTheKey_LosingOriginalCasing()
	{
		Assert.Equal("myphoto.jpg", new CloudFileName("MyPhoto.JPG").ToString());
	}

	/// <summary>
	/// H6 (fixed): lowercasing is invariant, so the object key no longer depends on the server's
	/// locale. Under tr-TR the culture-sensitive overload maps "I" to the dotless U+0131, which meant
	/// a Turkish-locale host wrote to a different key than a Danish one and could never read back what
	/// the other had written.
	/// </summary>
	[Fact]
	public void Lowercasing_IsCultureInvariant()
	{
		var original = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
			var turkish = new CloudFileName("IMAGE.jpg").Value;

			CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
			var invariant = new CloudFileName("IMAGE.jpg").Value;

			Assert.Equal("image.jpg", turkish);
			Assert.Equal("image.jpg", invariant);
			Assert.Equal(turkish, invariant);
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	/// <summary>
	/// H7 (fixed): CloudFileName is a struct, so default(CloudFileName) skips the constructor and every
	/// validation rule with it. Reading Value now fails loudly instead of yielding an empty object key
	/// that silently writes to the wrong place.
	/// </summary>
	[Fact]
	public void DefaultInstance_ThrowsOnValueAccess()
	{
		Assert.Throws<InvalidOperationException>(() => default(CloudFileName).Value);
		Assert.Throws<InvalidOperationException>(() => new CloudFileName().Value);

		Assert.False(default(CloudFileName).IsInitialized);
		Assert.True(new CloudFileName("photo.jpg").IsInitialized);
	}

	/// <summary>
	/// ToString stays non-throwing so debuggers and log statements remain safe; Value is the accessor
	/// that guarantees a real key.
	/// </summary>
	[Fact]
	public void DefaultInstance_ToStringRemainsSafe()
	{
		Assert.Equal(string.Empty, default(CloudFileName).ToString());
	}

	/// <summary>
	/// C4: UploadInfo carries a CloudFileName and is returned straight from callers' HTTP endpoints,
	/// so it must serialize as a plain JSON string rather than an object.
	/// </summary>
	[Fact]
	public void SerializesAsAPlainJsonString()
	{
		var json = System.Text.Json.JsonSerializer.Serialize(new CloudFileName("uploads/photo.jpg"));
		Assert.Equal("\"uploads/photo.jpg\"", json);

		var roundTripped = System.Text.Json.JsonSerializer.Deserialize<CloudFileName>(json);
		Assert.Equal(new CloudFileName("uploads/photo.jpg"), roundTripped);
	}

	/// <summary>
	/// Deserialization must not be a back door around validation. Returning `default` for a blank or
	/// malformed value would produce an uninitialised instance that survives the payload and only
	/// throws later, far from the cause -- reopening exactly the hole Value closes.
	/// </summary>
	[Theory]
	[InlineData("\"\"")]
	[InlineData("\"   \"")]
	[InlineData("null")]
	[InlineData("\"../traversal\"")]
	[InlineData("\"trailing/\"")]
	public void Deserialization_RejectsInvalidValues_RatherThanYieldingAnUninitialisedInstance(string json)
	{
		Assert.Throws<System.Text.Json.JsonException>(
			() => System.Text.Json.JsonSerializer.Deserialize<CloudFileName>(json));
	}

	/// <summary>
	/// L7 (fixed): Windows reserved device names mean nothing to an object store, and screening for
	/// them turned away legitimate keys. These are now accepted.
	/// </summary>
	[Theory]
	[InlineData("docs/aux/report.pdf")]
	[InlineData("con")]
	[InlineData("archive/lpt1/notes.txt")]
	[InlineData("reports/prn/2026.csv")]
	public void Accepts_WindowsReservedNames_WhichObjectStoresAllow(string name)
	{
		Assert.Equal(name.ToLowerInvariant(), new CloudFileName(name).Value);
	}

	/// <summary>
	/// L6 (fixed): the string conversion validates and can therefore throw, so it must be explicit
	/// rather than implicit (CA2225). A caller now has to opt in with a cast, which makes the
	/// possibility of failure visible at the call site.
	/// </summary>
	[Fact]
	public void ExplicitConversionFromString_Throws_OnInvalidInput()
	{
		Assert.Throws<ArgumentException>(() => (CloudFileName)"../traversal");
		Assert.Equal("photo.jpg", ((CloudFileName)"photo.jpg").Value);
	}

	[Fact]
	public void EqualityIsOrdinal_AfterNormalization()
	{
		var a = new CloudFileName("Photo.JPG");
		var b = new CloudFileName("photo.jpg");

		Assert.Equal(a, b);
		Assert.True(a == b);
		Assert.False(a != b);
		Assert.Equal(a.GetHashCode(), b.GetHashCode());
	}

	[Fact]
	public void ExplicitConversionToString_RoundTrips()
	{
		Assert.Equal("photo.jpg", (string)new CloudFileName("photo.jpg"));
	}
}
