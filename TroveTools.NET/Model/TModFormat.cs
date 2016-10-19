﻿using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TroveTools.NET.DataAccess;

namespace TroveTools.NET.Model
{
    static class TModFormat
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public const string AuthorValue = "author";
        public const string TitleValue = "title";
        public const string NotesValue = "notes";
        public const string PreviewPathValue = "previewPath";

        public static void ReadTmodProperties(string file, Dictionary<string, string> properties)
        {
            using (var stream = File.OpenRead(file))
            {
                using (var reader = new BinaryReader(stream))
                {
                    // Start at beginning of the file, read headerSize (fixed64), tmodVersion (fixed16), and propertyCount (fixed16)
                    stream.Position = 0;
                    ulong headerSize = reader.ReadUInt64();
                    ushort tmodVersion = reader.ReadUInt16();
                    ushort propertyCount = reader.ReadUInt16();

                    // Read a number of properties based on the propertyCount value
                    for (int i = 0; i < propertyCount; i++)
                    {
                        string key = reader.ReadString();
                        string value = reader.ReadString();

                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value)) properties[key] = value;
                    }
                }
            }
        }

        public static void ExtractTmod(string file, string folder, bool createOverrideFolders, Action<double> updateProgress)
        {
            var buffer = new byte[1048576];
            var properties = new Dictionary<string, string>();
            var archiveEntries = new List<ArchiveIndexEntry>();
            ulong headerSize = 0;
            ushort tmodVersion = 0, propertyCount = 0;

            using (var stream = File.OpenRead(file))
            {
                using (var reader = new MyBinaryReader(stream))
                {
                    // Start at beginning of the file, read headerSize (fixed64), tmodVersion (fixed16), and propertyCount (fixed16)
                    stream.Position = 0;
                    headerSize = reader.ReadUInt64();
                    tmodVersion = reader.ReadUInt16();
                    propertyCount = reader.ReadUInt16();

                    // Read a number of properties based on the propertyCount value
                    for (int i = 0; i < propertyCount; i++)
                    {
                        string key = reader.ReadString();
                        string value = reader.ReadString();

                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value)) properties[key] = value;
                    }

                    // Read archive index entries (remainder of header)
                    while ((ulong)stream.Position < headerSize)
                    {
                        var entry = new ArchiveIndexEntry();
                        entry.file = reader.ReadString();
                        entry.archiveIndex = reader.Read7BitEncodedInt();
                        entry.byteOffset = reader.Read7BitEncodedInt();
                        entry.size = reader.Read7BitEncodedInt();
                        entry.hash = reader.Read7BitEncodedInt();

                        archiveEntries.Add(entry);
                    }

                    int offset = 0, byteRead = 0;
                    double count = 0;
                    if (stream.Position != (long)headerSize) stream.Position = (long)headerSize;

                    using (InflaterInputStream decompressionStream = new InflaterInputStream(stream))
                    {
                        foreach (var entry in archiveEntries.OrderBy(e => e.byteOffset))
                        {
                            updateProgress(count / archiveEntries.Count * 100d);
                            log.InfoFormat("Extracting {0}", entry.file);
                            string extractPath = Path.Combine(folder, entry.file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
                            if (createOverrideFolders) extractPath = Path.Combine(Path.GetDirectoryName(extractPath), TroveMod.OverrideFolder, Path.GetFileName(extractPath));
                            SettingsDataProvider.ResolveFolder(Path.GetDirectoryName(extractPath));

                            // Advance data position to the next entry offset if needed
                            while (offset < entry.byteOffset && byteRead != -1)
                            {
                                byteRead = decompressionStream.ReadByte();
                                offset++;
                            }
                            offset += SaveBytes(extractPath, stream, decompressionStream, Convert.ToInt64(headerSize) + Convert.ToInt64(entry.byteOffset), entry.size, buffer);
                        }
                    }
                }
            }

            try
            {
                string title = properties.ContainsKey(TitleValue) ? properties[TitleValue] : Path.GetFileNameWithoutExtension(file);
                string yamlPath = Path.Combine(folder, SettingsDataProvider.GetSafeFilename(title) + ".yaml");
                log.InfoFormat("Generating YAML file: {0}", yamlPath);

                ModDetails details = new ModDetails()
                {
                    Author = properties[AuthorValue],
                    Title = properties[TitleValue],
                    Notes = properties[NotesValue],
                    PreviewPath = properties[PreviewPathValue],
                    Files = archiveEntries.Select(e => e.file).ToList()
                };
                details.SaveYamlFile(yamlPath);
            }
            catch (Exception ex) { log.Error("Error generating YAML file", ex); }

            log.InfoFormat("Completed extracting files from {0}", file);
        }

        private static int SaveBytes(string extractPath, FileStream stream, InflaterInputStream decompressionStream, long position, int size, byte[] buffer)
        {
            int bytesToRead = size, read = 0;
            try
            {
                try { if (File.Exists(extractPath)) File.Delete(extractPath); }
                catch { }

                using (FileStream output = File.Create(extractPath))
                {
                    //stream.Position = position;
                    do
                    {
                        read = decompressionStream.Read(buffer, 0, bytesToRead < buffer.Length ? bytesToRead : buffer.Length);
                        output.Write(buffer, 0, read);
                        bytesToRead -= read;
                    } while (read > 0 && bytesToRead > 0);
                }
                if (bytesToRead > 0) log.ErrorFormat("Error extracting {0}: {1} bytes left to read and 0 bytes read from source", extractPath, bytesToRead);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error extracting {0}", extractPath), ex);
            }
            return size - bytesToRead;
        }

        class ArchiveIndexEntry
        {
            public string file; // Original filename (in .tmods this includes directory separators)
            public int archiveIndex; // <- Should be 0
            public int byteOffset; // Offset into allTheData
            public int size; // Size in bytes
            public int hash; // Data integrity
        }
    }

    public class MyBinaryReader : BinaryReader
    {
        public MyBinaryReader(Stream stream) : base(stream) { }
        public new int Read7BitEncodedInt() { return base.Read7BitEncodedInt(); }
    }
}