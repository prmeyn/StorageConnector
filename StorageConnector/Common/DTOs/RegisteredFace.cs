using System.Text.Json.Serialization;

namespace StorageConnector.Common.DTOs
{
	/// <summary>
	/// The result of deliberately storing a biometric template.
	/// </summary>
	public sealed record RegisteredFace
	{
		/// <summary>
		/// The provider's identifier for the stored template. Useful for audit records showing what was
		/// stored and when.
		/// </summary>
		[JsonPropertyName("persistedFaceId")]
		public required string PersistedFaceId { get; init; }

		/// <summary>
		/// The caller's identifier for the person, as supplied to
		/// <see cref="IFaceRecognitionProvider.RegisterFaceAsync"/>. Pass this to
		/// <see cref="IFaceRecognitionProvider.DeleteRegisteredFacesAsync"/> to erase the template.
		/// </summary>
		[JsonPropertyName("subjectId")]
		public required string SubjectId { get; init; }
	}
}
