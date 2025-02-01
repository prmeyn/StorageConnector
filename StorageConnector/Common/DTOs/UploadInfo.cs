using System.Text.Json.Serialization;

namespace StorageConnector.Common.DTOs
{
	public sealed record UploadInfo
	{
		[JsonPropertyName("directUploadUrl")]
		public required string DirectUploadUrl { get; init; }
		[JsonPropertyName("method")]
		public required string HttpMethod { get; init; }

		[JsonPropertyName("headers")]
		public required Dictionary<string, string> Headers { get; init; }
	}
}
