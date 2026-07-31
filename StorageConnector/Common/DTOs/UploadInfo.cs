using System.Text.Json.Serialization;

namespace StorageConnector.Common.DTOs
{
	public sealed record UploadInfo
	{
		/// <summary>
		/// The object key the upload will actually be stored under.
		///
		/// This is not necessarily the name the caller asked for: the service appends the extension
		/// derived from the content type. Without it being reported back, a caller who requested
		/// "holiday/photo" had no way to learn that the bytes landed at "holiday/photo.jpg", so the
		/// matching call to GenerateDirectDownloadInfo could never find them (finding C4). Persist this
		/// value -- it is the handle to the uploaded object.
		/// </summary>
		[JsonPropertyName("fileName")]
		public required CloudFileName FileName { get; init; }

		[JsonPropertyName("directUploadUrl")]
		public required string DirectUploadUrl { get; init; }

		[JsonPropertyName("method")]
		public required string HttpMethod { get; init; }

		[JsonPropertyName("headers")]
		public required Dictionary<string, string> Headers { get; init; }
	}
}
