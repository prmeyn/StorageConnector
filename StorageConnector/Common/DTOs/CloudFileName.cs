using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace StorageConnector.Common.DTOs
{
	[JsonConverter(typeof(CloudFileNameJsonConverter))]
	public readonly partial struct CloudFileName : IEquatable<CloudFileName>
	{
		private readonly string? _value;

		public CloudFileName(string fileReferenceWithPath)
		{
			if (string.IsNullOrWhiteSpace(fileReferenceWithPath))
			{
				throw new ArgumentException("File reference cannot be null or empty.", nameof(fileReferenceWithPath));
			}

			if (!IsValidName(fileReferenceWithPath))
			{
				throw new ArgumentException($"Invalid file name: {fileReferenceWithPath}", nameof(fileReferenceWithPath));
			}

			// Invariant, not current-culture: under tr-TR the culture-sensitive overload maps 'I' to the
			// dotless 'ı', so the same input produced a different object key depending on the server's
			// locale, and a host in one locale could never read back what another had written (H6).
			_value = fileReferenceWithPath.ToLowerInvariant();
		}

		/// <summary>
		/// The validated object key.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// Thrown when this instance is <c>default(CloudFileName)</c>. Being a struct, the constructor --
		/// and therefore every validation rule -- can be bypassed entirely, which previously yielded an
		/// empty object key that silently wrote to the wrong place (H7). Library code reads this
		/// property rather than <see cref="ToString"/> so that never happens quietly.
		/// </exception>
		public string Value => _value
			?? throw new InvalidOperationException(
				$"This {nameof(CloudFileName)} was never initialised. Construct it with " +
				$"new {nameof(CloudFileName)}(\"path/to/file.ext\") rather than using default or new().");

		/// <summary>
		/// True when this instance was produced by the constructor rather than by <c>default</c>.
		/// </summary>
		public bool IsInitialized => _value is not null;

		private static bool IsValidName(string name)
		{
			if (string.IsNullOrEmpty(name) || name.Length > 1024)
				return false;

			if (name.IndexOfAny(['\0', '\n', '\r']) != -1)
				return false;

			if (name.Contains("../", StringComparison.Ordinal) || name.StartsWith("./", StringComparison.Ordinal))
				return false;

			// Windows reserved device names (con, aux, prn, lpt1 ...) are deliberately NOT rejected.
			// They mean nothing to an object store, and screening for them turned away legitimate keys
			// such as "docs/aux/report.pdf" (finding L7).

			if (name.StartsWith('/') || name.EndsWith('/') || name.EndsWith('.'))
				return false;

			if (name.Contains("//", StringComparison.Ordinal))
				return false;

			var invalidCharPattern = ValidationRegex();
			return !invalidCharPattern.IsMatch(name);
		}

		public override readonly bool Equals(object? obj)
		{
			return obj is CloudFileName other && Equals(other);
		}

		public override readonly int GetHashCode()
		{
			return _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
		}

		public readonly bool Equals(CloudFileName other)
		{
			return string.Equals(_value, other._value, StringComparison.Ordinal);
		}

		/// <summary>
		/// Returns the object key, or an empty string when uninitialised. Kept non-throwing so
		/// debuggers and logging remain safe; use <see cref="Value"/> when the key actually matters.
		/// </summary>
		public override readonly string ToString()
		{
			return _value ?? string.Empty;
		}

		/// <summary>
		/// Explicit, not implicit: this conversion validates and therefore throws, and a conversion that
		/// can throw must never be implicit (CA2225, finding L6). Write
		/// <c>new CloudFileName(name)</c> or <c>(CloudFileName)name</c>.
		/// </summary>
		public static explicit operator CloudFileName(string value)
		{
			return new CloudFileName(value);
		}

		public static explicit operator string(CloudFileName cloudFileName)
		{
			return cloudFileName.Value;
		}

		public static bool operator ==(CloudFileName left, CloudFileName right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(CloudFileName left, CloudFileName right)
		{
			return !left.Equals(right);
		}

		[GeneratedRegex(@"[<>:""\\|?*]")]
		private static partial Regex ValidationRegex();
	}

	/// <summary>
	/// Serializes <see cref="CloudFileName"/> as a plain JSON string. Without this the struct would
	/// serialize as an object, which matters because <c>UploadInfo</c> carries one and is returned
	/// directly from callers' HTTP endpoints.
	/// </summary>
	public sealed class CloudFileNameJsonConverter : JsonConverter<CloudFileName>
	{
		public override CloudFileName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = reader.GetString();

			// Returning `default` for a missing or blank value would reopen the very hole the Value
			// property closes: an uninitialised instance that survives deserialization and only throws
			// later, far from the malformed payload that caused it. Fail here instead.
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new JsonException($"A {nameof(CloudFileName)} cannot be null or blank.");
			}

			try
			{
				return new CloudFileName(value);
			}
			catch (ArgumentException ex)
			{
				throw new JsonException($"'{value}' is not a valid {nameof(CloudFileName)}: {ex.Message}", ex);
			}
		}

		public override void Write(Utf8JsonWriter writer, CloudFileName value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToString());
		}
	}
}
