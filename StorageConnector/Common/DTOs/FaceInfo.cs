using System.Text.Json.Serialization;

namespace StorageConnector.Common.DTOs
{
	public sealed record FaceInfo
	{
		[JsonPropertyName("numberOfFaces")]
		public required byte NumberOfFaces { get; init; }
		[JsonPropertyName("matchingUserData")]
		public required HashSet<string> MatchingUserData { get; init; }
	}
}
