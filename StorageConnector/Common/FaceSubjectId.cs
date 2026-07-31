using System.Text.RegularExpressions;

namespace StorageConnector.Common
{
	/// <summary>
	/// Validates the caller's identifier for a registered person.
	///
	/// AWS Rekognition restricts <c>ExternalImageId</c> to <c>[a-zA-Z0-9_.\-:]+</c>, so the arbitrary
	/// string previously passed straight through would be rejected by the service for anything as
	/// ordinary as an email address or a name with a space (finding H5). The same rule is applied to
	/// every provider so a subject id means the same thing whichever cloud is configured.
	/// </summary>
	internal static partial class FaceSubjectId
	{
		internal const int MaxLength = 255;

		internal static string Validate(string subjectId, string parameterName)
		{
			if (string.IsNullOrWhiteSpace(subjectId))
			{
				throw new ArgumentException("A subject identifier is required.", parameterName);
			}

			if (subjectId.Length > MaxLength)
			{
				throw new ArgumentException(
					$"Subject identifier must be at most {MaxLength} characters; got {subjectId.Length}.", parameterName);
			}

			if (!AllowedPattern().IsMatch(subjectId))
			{
				throw new ArgumentException(
					$"Subject identifier '{subjectId}' contains unsupported characters. Use only letters, digits, " +
					"underscore, dot, hyphen and colon -- for example a user id or a hashed reference rather than " +
					"an email address or display name.",
					parameterName);
			}

			return subjectId;
		}

		[GeneratedRegex(@"^[a-zA-Z0-9_.\-:]+$")]
		private static partial Regex AllowedPattern();
	}
}
