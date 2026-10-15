using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace YARG.Online
{
    /// <summary>Packages a selected song and extracts peer downloads into an isolated cache.</summary>
    public static class SongPackage
    {
        public const long MAX_PACKAGE_BYTES = 1024L * 1024 * 1024;
        public const int MAX_FILES = 128;

        private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".ini", ".mid", ".midi", ".chart", ".txt", ".ogg", ".opus", ".mp3", ".wav", ".flac",
            ".png", ".jpg", ".jpeg", ".sng", ".yargsong"
        };

        public static string Digest(string filename)
        {
            using var stream = File.OpenRead(filename);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        public static void Create(string location, string destination)
        {
            string[] files;
            if (Directory.Exists(location))
            {
                files = Directory.GetFiles(location).Where(IsAllowedFile).ToArray();
            }
            else if (File.Exists(location) &&
                (Path.GetExtension(location).Equals(".sng", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetExtension(location).Equals(".yargsong", StringComparison.OrdinalIgnoreCase)))
            {
                files = new[] { location };
            }
            else
            {
                throw new InvalidOperationException("Automatic song sharing supports song folders and SNG/YARGSONG files. " +
                    "For this song format, both players need their own copy.");
            }

            if (files.Length == 0 || files.Length > MAX_FILES || files.Sum(f => new FileInfo(f).Length) > MAX_PACKAGE_BYTES)
                throw new InvalidDataException("The selected song is too large to share in an online room.");

            using var output = File.Create(destination);
            using var archive = new ZipArchive(output, ZipArchiveMode.Create);
            foreach (string filename in files)
            {
                // Audio is already compressed. Storing it avoids a long CPU-bound preparation step.
                if (!IsPortableName(Path.GetFileName(filename)))
                    throw new InvalidDataException("A song filename cannot be shared between Windows and Linux.");
                var entry = archive.CreateEntry(Path.GetFileName(filename), CompressionLevel.NoCompression);
                using var source = File.OpenRead(filename);
                using var target = entry.Open();
                source.CopyTo(target);
            }
        }

        public static void Extract(string package, string destination, string expectedDigest)
        {
            if (new FileInfo(package).Length > MAX_PACKAGE_BYTES ||
                !string.Equals(Digest(package), expectedDigest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded song did not pass its integrity check.");

            using var archive = ZipFile.OpenRead(package);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MAX_FILES)
                throw new InvalidDataException("Invalid song package file count.");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long bytes = 0;
            // Validate the entire archive before creating any files.
            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName;
                if (!IsPortableName(name) ||
                    !IsAllowedFile(name) || !names.Add(name) || entry.Length < 0 ||
                    entry.Length > MAX_PACKAGE_BYTES - bytes)
                    throw new InvalidDataException("The downloaded song contains an invalid file.");
                bytes += entry.Length;
            }

            Directory.CreateDirectory(destination);
            foreach (var entry in archive.Entries)
            {
                // FileMode.CreateNew also protects a pre-existing cache from accidental replacement.
                using var target = new FileStream(Path.Combine(destination, entry.FullName), FileMode.CreateNew);
                using var source = entry.Open();
                var buffer = new byte[65536];
                long remaining = entry.Length;
                int read;
                while ((read = source.Read(buffer, 0, (int) Math.Min(buffer.Length, remaining + 1))) != 0)
                {
                    if (read > remaining) throw new InvalidDataException("The downloaded file exceeded its declared size.");
                    target.Write(buffer, 0, read);
                    remaining -= read;
                }
                if (remaining != 0) throw new InvalidDataException("The downloaded file was truncated.");
            }
        }

        private static bool IsPortableName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 200 || name.EndsWith(".") || name.EndsWith(" ") ||
                name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c))) return false;
            string stem = name.Split('.')[0].ToUpperInvariant();
            return stem != "CON" && stem != "PRN" && stem != "AUX" && stem != "NUL" &&
                !(stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
                  stem[3] >= '1' && stem[3] <= '9');
        }

        private static bool IsAllowedFile(string filename) => _extensions.Contains(Path.GetExtension(filename));
    }
}