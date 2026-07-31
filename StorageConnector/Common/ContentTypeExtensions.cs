using System.Collections.Frozen;
using Microsoft.AspNetCore.StaticFiles;

namespace StorageConnector.Common
{
	/// <summary>
	/// Resolves a content type to the file extension used in the stored object key.
	///
	/// This replaces a reverse scan of <see cref="FileExtensionContentTypeProvider"/>, which was wrong
	/// in two ways (finding H3): the extension-to-MIME table is many-to-one, so scanning it backwards
	/// returned whichever entry happened to enumerate first -- <c>image/jpeg</c> resolved to
	/// <c>.jpe</c> and <c>text/plain</c> to <c>.asm</c> -- and its coverage is incomplete, so ordinary
	/// types including <c>image/heic</c>, <c>text/csv</c>, <c>application/zip</c> and
	/// <c>application/xml</c> resolved to null, which aborted the upload entirely.
	///
	/// The curated map below is the authority for the canonical extension. The provider is still
	/// consulted as a fallback so breadth is not lost, but it is now built once rather than per call.
	/// </summary>
	public static class ContentTypeExtensions
	{
		private static readonly FileExtensionContentTypeProvider Provider = new();

		private static readonly FrozenDictionary<string, string> CanonicalExtensions =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				// Images
				["image/jpeg"] = ".jpg",
				["image/pjpeg"] = ".jpg",
				["image/png"] = ".png",
				["image/gif"] = ".gif",
				["image/webp"] = ".webp",
				["image/avif"] = ".avif",
				["image/heic"] = ".heic",
				["image/heif"] = ".heif",
				["image/bmp"] = ".bmp",
				["image/tiff"] = ".tiff",
				["image/svg+xml"] = ".svg",
				["image/x-icon"] = ".ico",
				["image/vnd.microsoft.icon"] = ".ico",

				// Documents
				["application/pdf"] = ".pdf",
				["application/msword"] = ".doc",
				["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
				["application/vnd.ms-excel"] = ".xls",
				["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
				["application/vnd.ms-powerpoint"] = ".ppt",
				["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
				["application/vnd.oasis.opendocument.text"] = ".odt",
				["application/vnd.oasis.opendocument.spreadsheet"] = ".ods",
				["application/rtf"] = ".rtf",

				// Text and data
				["text/plain"] = ".txt",
				["text/csv"] = ".csv",
				["text/markdown"] = ".md",
				["text/html"] = ".html",
				["text/css"] = ".css",
				["text/xml"] = ".xml",
				["application/xml"] = ".xml",
				["application/json"] = ".json",
				["application/x-ndjson"] = ".ndjson",
				["application/javascript"] = ".js",
				["text/javascript"] = ".js",
				["application/yaml"] = ".yaml",
				["text/yaml"] = ".yaml",

				// Archives
				["application/zip"] = ".zip",
				["application/x-zip-compressed"] = ".zip",
				["application/gzip"] = ".gz",
				["application/x-gzip"] = ".gz",
				["application/x-tar"] = ".tar",
				["application/x-7z-compressed"] = ".7z",
				["application/vnd.rar"] = ".rar",

				// Audio
				["audio/mpeg"] = ".mp3",
				["audio/mp4"] = ".m4a",
				["audio/aac"] = ".aac",
				["audio/wav"] = ".wav",
				["audio/x-wav"] = ".wav",
				["audio/ogg"] = ".ogg",
				["audio/flac"] = ".flac",
				["audio/webm"] = ".weba",

				// Video
				["video/mp4"] = ".mp4",
				["video/webm"] = ".webm",
				["video/quicktime"] = ".mov",
				["video/x-msvideo"] = ".avi",
				["video/x-matroska"] = ".mkv",
				["video/mpeg"] = ".mpeg",

				// Generic
				["application/octet-stream"] = ".bin",
			}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Returns the canonical file extension for <paramref name="contentType"/> (including the
		/// leading dot), or <c>null</c> when the type is not recognised.
		/// </summary>
		public static string? GetExtensionFromContentType(string? contentType)
		{
			if (string.IsNullOrWhiteSpace(contentType))
			{
				return null;
			}

			// Tolerate parameters such as "text/plain; charset=utf-8".
			var separatorIndex = contentType.IndexOf(';');
			var mediaType = (separatorIndex >= 0 ? contentType[..separatorIndex] : contentType).Trim();

			if (CanonicalExtensions.TryGetValue(mediaType, out var extension))
			{
				return extension;
			}

			foreach (var mapping in Provider.Mappings)
			{
				if (mapping.Value.Equals(mediaType, StringComparison.OrdinalIgnoreCase))
				{
					return mapping.Key;
				}
			}

			return null;
		}
	}
}
