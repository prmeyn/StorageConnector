namespace StorageConnector.Services.AWS
{
	/// <summary>
	/// Thresholds for what counts as a usable face, and how close a match must be.
	///
	/// These were hard-coded constants inside the detection method (finding M9). Bound from
	/// <c>StorageConnectors:AWS:FaceQuality</c>; every value is optional and falls back to the previous
	/// hard-coded default, so existing configurations behave exactly as before.
	/// </summary>
	public sealed class FaceQualitySettings
	{
		public float MinSharpness { get; init; } = 50.0f;

		public float MinBrightness { get; init; } = 30.0f;

		/// <summary>Minimum confidence that a detected region really is a face.</summary>
		public float MinConfidence { get; init; } = 80.0f;

		/// <summary>
		/// How similar a face must be to count as the same person, 0-100. Lowering this increases false
		/// matches, which for an identity check means matching the wrong person.
		/// </summary>
		public float MatchThreshold { get; init; } = 98.0f;
	}
}
