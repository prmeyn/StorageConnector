using System.Text.Json.Serialization;

namespace StorageConnector.Common.DTOs
{
	public sealed record DownloadInfo
	{
		[JsonPropertyName("directDownloadUrl")]
		public required string DirectDownloadUrl { get; init; }
	}
}
