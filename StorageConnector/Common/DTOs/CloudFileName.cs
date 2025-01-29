using System.Text.RegularExpressions;

namespace StorageConnector.Common.DTOs
{
	public partial struct CloudFileName : IEquatable<CloudFileName>
	{
		private readonly string _value;

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

			_value = fileReferenceWithPath.ToLower();
		}

		private static bool IsValidName(string name)
		{
			if (string.IsNullOrEmpty(name) || name.Length < 1 || name.Length > 1024)
				return false;

			if (name.IndexOfAny(new char[] { '\0', '\n', '\r' }) != -1)
				return false;

			if (name.Contains("../") || name.Contains("..\\") || name.StartsWith("./") || name.StartsWith(".\\"))
				return false;

			var reservedNames = new[] { "con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9" };
			if (Array.Exists(reservedNames, reservedName => name.ToLower().Split('.','/').Contains(reservedName)))
				return false;

			if (name.StartsWith("/") || name.EndsWith("/") || name.EndsWith("."))
				return false;

			if (name.Contains("//"))
				return false;

			var invalidCharPattern = ValidationRegex();
			return !invalidCharPattern.IsMatch(name);
		}

		public override bool Equals(object obj)
		{
			return obj is CloudFileName other && Equals(other);
		}

		public override int GetHashCode()
		{
			return _value?.GetHashCode() ?? 0;
		}

		public bool Equals(CloudFileName other)
		{
			return string.Equals(_value, other._value, StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return _value ?? string.Empty;
		}

		public static implicit operator CloudFileName(string value)
		{
			return new CloudFileName(value);
		}

		public static explicit operator string(CloudFileName cloudFileName)
		{
			return cloudFileName._value;
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

}
