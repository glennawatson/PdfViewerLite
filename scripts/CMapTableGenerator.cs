#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Builds the packed predefined CMap and CID-to-Unicode tables in src/HyperPdfLibrary/Fonts/Data/CMaps from Adobe's
// cmap-resources (https://github.com/adobe-type-tools/cmap-resources, BSD 3-Clause): the CMap files themselves and the
// Unicode columns of each collection's cid2code.txt, plus the Adobe-*-UCS2 ToUnicode maps of Adobe's
// mapping-resources-pdf (https://github.com/adobe-type-tools/mapping-resources-pdf, BSD 3-Clause), which win where
// they have a value. The output is the format PredefinedCMaps and CidToUnicodeTable
// read. The compare mode checks two folders of packed tables by the lookups they answer.
// Usage: dotnet run --file scripts/CMapTableGenerator.cs -- generate <cmap-resources folder> <mapping-resources-pdf folder> <output folder>
//        dotnet run --file scripts/CMapTableGenerator.cs -- compare <old folder> <new folder>
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

return PdfViewerLite.Scripts.CMapTableGenerator.Run(args);

namespace PdfViewerLite.Scripts
{
    /// <summary>Builds and compares the packed CMap tables.</summary>
    internal static class CMapTableGenerator
    {
        /// <summary>The exit code when the run succeeded.</summary>
        private const int Success = 0;

        /// <summary>The exit code when the arguments are wrong.</summary>
        private const int UsageError = 2;

        /// <summary>The exit code when the compared tables differ in a lookup the old tables answered.</summary>
        private const int Different = 3;

        /// <summary>The number of arguments of a mode.</summary>
        private const int ArgumentCount = 3;

        /// <summary>The number of arguments of the generate mode.</summary>
        private const int GenerateArgumentCount = 4;

        /// <summary>How a value differs between two tables.</summary>
        internal enum Outcome
        {
            /// <summary>Neither table has a value.</summary>
            None = 0,

            /// <summary>Both tables have the same value.</summary>
            Same = 1,

            /// <summary>Only the new table has a value.</summary>
            Added = 2,

            /// <summary>Only the old table has a value.</summary>
            Lost = 3,

            /// <summary>Both tables have different values.</summary>
            Changed = 4,
        }

        /// <summary>Runs the tool.</summary>
        /// <param name="args">The command line.</param>
        /// <returns>The exit code.</returns>
        internal static int Run(string[] args)
        {
            if (args.Length == GenerateArgumentCount && args[0] == "generate")
            {
                Generate(args[1], args[2], args[3]);
                return Success;
            }

            if (args.Length == ArgumentCount && args[0] == "compare")
            {
                return TableComparer.Compare(args[1], args[2]) ? Success : Different;
            }

            Console.Error.WriteLine("Usage: CMapTableGenerator generate <cmap-resources folder> <mapping-resources-pdf folder> <output folder>");
            Console.Error.WriteLine("       CMapTableGenerator compare <old folder> <new folder>");
            return UsageError;
        }

        /// <summary>Writes every packed table.</summary>
        /// <param name="source">The cmap-resources folder.</param>
        /// <param name="mappings">The mapping-resources-pdf folder.</param>
        /// <param name="output">The output folder.</param>
        private static void Generate(string source, string mappings, string output)
        {
            _ = Directory.CreateDirectory(output);
            foreach (var collection in Collections.All)
            {
                var maps = AdobeCMapParser.ParseAll(Path.Combine(source, collection.Folder, "CMap"), collection.CMaps);
                foreach (var name in collection.CMaps)
                {
                    PackedTables.Write(Path.Combine(output, $"{name}.bin"), PackedTables.PackCMap(maps, name, collection));
                }

                var fill = Cid2Code.Load(Path.Combine(source, collection.Folder, "cid2code.txt"), collection);
                var values = ToUnicodeMap.Merge(Path.Combine(mappings, "pdf2unicode", collection.UnicodeTable), fill);
                PackedTables.Write(Path.Combine(output, $"{collection.UnicodeTable}.bin"), PackedTables.PackUnicode(values));
            }
        }

        /// <summary>A range of codes that map to consecutive CIDs.</summary>
        /// <param name="Low">The first code.</param>
        /// <param name="High">The last code.</param>
        /// <param name="Cid">The CID of the first code.</param>
        internal readonly record struct CidRange(uint Low, uint High, int Cid);

        /// <summary>A codespace range.</summary>
        /// <param name="Length">The code length in bytes.</param>
        /// <param name="Low">The first code.</param>
        /// <param name="High">The last code.</param>
        internal readonly record struct Codespace(int Length, uint Low, uint High);

        /// <summary>The counts of a comparison.</summary>
        /// <param name="Same">Entries in both with the same value.</param>
        /// <param name="Added">Entries only in the new table.</param>
        /// <param name="Lost">Entries only in the old table.</param>
        /// <param name="Changed">Entries in both with different values.</param>
        internal readonly record struct Counts(int Same, int Added, int Lost, int Changed);

        /// <summary>The collections and CMaps packed into the library.</summary>
        internal static class Collections
        {
            /// <summary>The CJK script value of Adobe-Japan1.</summary>
            private const byte Japan1 = 1;

            /// <summary>The CJK script value of Adobe-GB1.</summary>
            private const byte GB1 = 2;

            /// <summary>The CJK script value of Adobe-CNS1.</summary>
            private const byte CNS1 = 3;

            /// <summary>The CJK script value of Adobe-Korea1.</summary>
            private const byte Korea1 = 4;

            /// <summary>Gets the collections.</summary>
            internal static Collection[] All { get; } =
            [
                new(
                    "Adobe-Japan1-7",
                    Japan1,
                    "Adobe-Japan1-UCS2",
                    ["UniJIS-UCS2", "UniJIS-UCS2-HW", "UniJIS-UTF16", "UniJIS2004-UTF16", "UniJISX0213-UTF32", "UniJISX02132004-UTF32"],
                    [
                        "83pv-RKSJ-H", "90msp-RKSJ-H", "90msp-RKSJ-V", "90ms-RKSJ-H", "90ms-RKSJ-V", "90pv-RKSJ-H", "Add-RKSJ-H",
                        "Add-RKSJ-V", "EUC-H", "EUC-V", "Ext-RKSJ-H", "Ext-RKSJ-V", "H", "UniJIS-UCS2-H", "UniJIS-UCS2-HW-H",
                        "UniJIS-UCS2-HW-V", "UniJIS-UCS2-V", "UniJIS-UTF16-H", "UniJIS-UTF16-V", "UniJIS-UTF32-H", "UniJIS-UTF32-V", "V",
                    ]),
                new(
                    "Adobe-GB1-6",
                    GB1,
                    "Adobe-GB1-UCS2",
                    ["UniGB-UCS2", "UniGB-UTF16"],
                    [
                        "GB-EUC-H", "GB-EUC-V", "GBK2K-H", "GBK2K-V", "GBK-EUC-H", "GBK-EUC-V", "GBKp-EUC-H", "GBKp-EUC-V",
                        "GBpc-EUC-H", "GBpc-EUC-V", "UniGB-UCS2-H", "UniGB-UCS2-V", "UniGB-UTF16-H", "UniGB-UTF16-V",
                        "UniGB-UTF32-H", "UniGB-UTF32-V",
                    ]),
                new(
                    "Adobe-CNS1-7",
                    CNS1,
                    "Adobe-CNS1-UCS2",
                    ["UniCNS-UCS2", "UniCNS-UTF16"],
                    [
                        "B5pc-H", "B5pc-V", "CNS-EUC-H", "CNS-EUC-V", "ETen-B5-H", "ETen-B5-V", "ETenms-B5-H", "ETenms-B5-V",
                        "HKscs-B5-H", "HKscs-B5-V", "UniCNS-UCS2-H", "UniCNS-UCS2-V", "UniCNS-UTF16-H", "UniCNS-UTF16-V",
                        "UniCNS-UTF32-H", "UniCNS-UTF32-V",
                    ]),
                new(
                    "Adobe-Korea1-2",
                    Korea1,
                    "Adobe-Korea1-UCS2",
                    ["UniKS-UCS2", "UniKS-UTF16"],
                    [
                        "KSC-EUC-H", "KSC-EUC-V", "KSCms-UHC-H", "KSCms-UHC-HW-H", "KSCms-UHC-HW-V", "KSCms-UHC-V", "KSCpc-EUC-H",
                        "UniKS-UCS2-H", "UniKS-UCS2-V", "UniKS-UTF16-H", "UniKS-UTF16-V", "UniKS-UTF32-H", "UniKS-UTF32-V",
                    ]),
            ];
        }

        /// <summary>Reads Adobe's CMap files.</summary>
        internal static class AdobeCMapParser
        {
            /// <summary>The first character of a hexadecimal string.</summary>
            private const char HexOpen = '<';

            /// <summary>The number of hex digits per byte.</summary>
            private const int DigitsPerByte = 2;

            /// <summary>The number of tokens of a CID range entry.</summary>
            private const int RangeTokens = 3;

            /// <summary>The number of tokens of a CID character entry.</summary>
            private const int CharTokens = 2;

            /// <summary>The comment marker.</summary>
            private const char Comment = '%';

            /// <summary>Parses the CMaps of a collection.</summary>
            /// <param name="folder">The collection's CMap folder.</param>
            /// <param name="names">The CMap names.</param>
            /// <returns>The CMaps by name, including every CMap one of them builds on.</returns>
            internal static Dictionary<string, AdobeCMap> ParseAll(string folder, string[] names)
            {
                var maps = new Dictionary<string, AdobeCMap>(StringComparer.Ordinal);
                foreach (var name in names)
                {
                    AddWithParents(folder, name, maps);
                }

                return maps;
            }

            /// <summary>Parses a CMap and the CMaps it builds on.</summary>
            /// <param name="folder">The CMap folder.</param>
            /// <param name="name">The CMap name.</param>
            /// <param name="maps">Receives the CMaps.</param>
            private static void AddWithParents(string folder, string name, Dictionary<string, AdobeCMap> maps)
            {
                if (maps.ContainsKey(name))
                {
                    return;
                }

                var map = Parse(Path.Combine(folder, name));
                maps[name] = map;
                if (map.Parent.Length > 0)
                {
                    AddWithParents(folder, map.Parent, maps);
                }
            }

            /// <summary>Parses a CMap file.</summary>
            /// <param name="path">The file.</param>
            /// <returns>The CMap.</returns>
            private static AdobeCMap Parse(string path)
            {
                var map = new AdobeCMap(string.Empty, false, [], []);
                var tokens = Tokens(File.ReadAllLines(path, Encoding.ASCII));
                var parent = string.Empty;
                var vertical = false;
                for (var i = 0; i < tokens.Count; i++)
                {
                    switch (tokens[i])
                    {
                        case "usecmap":
                        {
                            parent = tokens[i - 1].TrimStart('/');
                            break;
                        }

                        case "/WMode":
                        {
                            vertical = tokens[i + 1] == "1";
                            break;
                        }

                        case "begincodespacerange":
                        {
                            i = ReadCodespaces(tokens, i + 1, map.Codespaces);
                            break;
                        }

                        case "begincidrange":
                        {
                            i = ReadRanges(tokens, i + 1, map.Ranges, true);
                            break;
                        }

                        case "begincidchar":
                        {
                            i = ReadRanges(tokens, i + 1, map.Ranges, false);
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }

                return map with { Parent = parent, Vertical = vertical };
            }

            /// <summary>Splits lines into tokens, dropping comments.</summary>
            /// <param name="lines">The lines.</param>
            /// <returns>The tokens.</returns>
            private static List<string> Tokens(string[] lines)
            {
                var tokens = new List<string>();
                foreach (var line in lines)
                {
                    var comment = line.IndexOf(Comment, StringComparison.Ordinal);
                    var text = comment >= 0 ? line[..comment] : line;
                    tokens.AddRange(text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                }

                return tokens;
            }

            /// <summary>Reads the codespace ranges of a block.</summary>
            /// <param name="tokens">The tokens.</param>
            /// <param name="start">The first token after the begin keyword.</param>
            /// <param name="output">Receives the ranges.</param>
            /// <returns>The index of the end keyword.</returns>
            private static int ReadCodespaces(List<string> tokens, int start, List<Codespace> output)
            {
                var i = start;
                while (tokens[i][0] == HexOpen)
                {
                    output.Add(new((tokens[i].Length / DigitsPerByte) - 1, Hex(tokens[i]), Hex(tokens[i + 1])));
                    i += CharTokens;
                }

                return i;
            }

            /// <summary>Reads the CID ranges or characters of a block.</summary>
            /// <param name="tokens">The tokens.</param>
            /// <param name="start">The first token after the begin keyword.</param>
            /// <param name="output">Receives the ranges.</param>
            /// <param name="ranges">Whether entries are ranges rather than single characters.</param>
            /// <returns>The index of the end keyword.</returns>
            private static int ReadRanges(List<string> tokens, int start, List<CidRange> output, bool ranges)
            {
                var step = ranges ? RangeTokens : CharTokens;
                var i = start;
                while (tokens[i][0] == HexOpen)
                {
                    var low = Hex(tokens[i]);
                    var high = ranges ? Hex(tokens[i + 1]) : low;
                    var cid = int.Parse(tokens[i + step - 1], CultureInfo.InvariantCulture);
                    output.Add(new(low, high, cid));
                    i += step;
                }

                return i;
            }

            /// <summary>Parses a hexadecimal string such as <c>&lt;8140&gt;</c>.</summary>
            /// <param name="token">The token.</param>
            /// <returns>The code.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint Hex(string token) =>
                uint.Parse(token.AsSpan(1, token.Length - DigitsPerByte), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads the Adobe-*-UCS2 ToUnicode maps of mapping-resources-pdf.</summary>
        internal static class ToUnicodeMap
        {
            /// <summary>The number of tokens of a bfchar entry.</summary>
            private const int CharTokens = 2;

            /// <summary>The number of tokens of a bfrange entry.</summary>
            private const int RangeTokens = 3;

            /// <summary>The number of hex digits of one UTF-16 code unit.</summary>
            private const int UnitDigits = 4;

            /// <summary>The number of hex digits in the angle brackets of a hex string, plus the brackets.</summary>
            private const int BracketChars = 2;

            /// <summary>Reads the map and fills the CIDs it lacks from another table.</summary>
            /// <param name="path">The ToUnicode CMap.</param>
            /// <param name="fill">The values to use where the map has none; indexed by CID.</param>
            /// <returns>The merged values, as long as the longer of the two.</returns>
            internal static char[] Merge(string path, char[] fill)
            {
                var map = Read(path);
                var values = new char[Math.Max(map.Length, fill.Length)];
                for (var cid = 0; cid < values.Length; cid++)
                {
                    var preferred = cid < map.Length ? map[cid] : '\0';
                    var fallback = cid < fill.Length ? fill[cid] : '\0';
                    values[cid] = preferred != '\0' ? preferred : fallback;
                }

                return values;
            }

            /// <summary>Reads the single-character mappings of the map; multi-character ones are skipped.</summary>
            /// <param name="path">The ToUnicode CMap.</param>
            /// <returns>The value of each CID up to the highest mapped.</returns>
            private static char[] Read(string path)
            {
                var values = new char[ushort.MaxValue + 1];
                var tokens = new List<string>();
                foreach (var line in File.ReadLines(path, Encoding.ASCII))
                {
                    var text = line.Length > 0 && line[0] == '%' ? string.Empty : line;
                    tokens.AddRange(text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                }

                var last = 0;
                for (var i = 0; i < tokens.Count; i++)
                {
                    var isChar = tokens[i] == "beginbfchar";
                    if (isChar || tokens[i] == "beginbfrange")
                    {
                        i = ReadBlock(tokens, i + 1, isChar, values);
                    }
                }

                for (var cid = 0; cid < values.Length; cid++)
                {
                    last = values[cid] != '\0' ? cid : last;
                }

                return values[..(last + 1)];
            }

            /// <summary>Reads the entries of a bfchar or bfrange block.</summary>
            /// <param name="tokens">The tokens.</param>
            /// <param name="start">The first token after the begin keyword.</param>
            /// <param name="single">Whether entries are single characters rather than ranges.</param>
            /// <param name="values">Receives the values.</param>
            /// <returns>The index of the end keyword.</returns>
            private static int ReadBlock(List<string> tokens, int start, bool single, char[] values)
            {
                var step = single ? CharTokens : RangeTokens;
                var i = start;
                while (tokens[i][0] == '<')
                {
                    var low = Hex(tokens[i]);
                    var high = single ? low : Hex(tokens[i + 1]);
                    var destination = tokens[i + step - 1];
                    var first = int.Parse(destination.AsSpan(1, UnitDigits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                    var simple = single || destination.Length - BracketChars == UnitDigits;
                    if (simple && !char.IsSurrogate((char)first))
                    {
                        for (var cid = low; cid <= high; cid++)
                        {
                            values[cid] = (char)(first + (cid - low));
                        }
                    }

                    i += step;
                }

                return i;
            }

            /// <summary>Parses a hexadecimal string such as <c>&lt;00a6&gt;</c>.</summary>
            /// <param name="token">The token.</param>
            /// <returns>The value.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int Hex(string token) =>
                int.Parse(token.AsSpan(1, token.Length - BracketChars), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }

        /// <summary>Reads the Unicode columns of a collection's cid2code.txt.</summary>
        internal static class Cid2Code
        {
            /// <summary>The marker of a CID the column does not use.</summary>
            private const string Unused = "*";

            /// <summary>The suffix of a vertical-only code.</summary>
            private const char VerticalSuffix = 'v';

            /// <summary>The comment marker of the file.</summary>
            private const char Comment = '#';

            /// <summary>The first CJK Radicals Supplement code, the start of the radical forms.</summary>
            private const int RadicalsFirst = 0x2E80;

            /// <summary>The last Kangxi Radicals code.</summary>
            private const int RadicalsLast = 0x2FDF;

            /// <summary>The first vertical presentation form code.</summary>
            private const int VerticalFormsFirst = 0xFE10;

            /// <summary>The last vertical presentation form code of the first block.</summary>
            private const int VerticalFormsFirstLast = 0xFE1F;

            /// <summary>The first CJK compatibility form code.</summary>
            private const int CompatFormsFirst = 0xFE30;

            /// <summary>The last CJK compatibility form code.</summary>
            private const int CompatFormsLast = 0xFE4F;

            /// <summary>The first private-use code.</summary>
            private const int PrivateFirst = 0xE000;

            /// <summary>The last compatibility ideograph code.</summary>
            private const int CompatIdeographsLast = 0xFAFF;

            /// <summary>Loads the Unicode value of every CID.</summary>
            /// <param name="path">The cid2code.txt file.</param>
            /// <param name="collection">The collection.</param>
            /// <returns>The value of each CID; zero where the collection has none.</returns>
            /// <exception cref="InvalidDataException">A Unicode column is missing.</exception>
            internal static char[] Load(string path, Collection collection)
            {
                var rows = ReadRows(path);
                var header = rows[0].Split('\t');
                var columns = new int[collection.UnicodeColumns.Length];
                for (var i = 0; i < columns.Length; i++)
                {
                    columns[i] = Array.IndexOf(header, collection.UnicodeColumns[i]);
                    if (columns[i] < 0)
                    {
                        throw new InvalidDataException($"{path} has no {collection.UnicodeColumns[i]} column.");
                    }
                }

                var values = new char[rows.Count - 1];
                for (var i = 1; i < rows.Count; i++)
                {
                    var fields = rows[i].Split('\t');
                    values[int.Parse(fields[0], CultureInfo.InvariantCulture)] = Pick(fields, columns);
                }

                return values;
            }

            /// <summary>Reads the table rows, header first.</summary>
            /// <param name="path">The file.</param>
            /// <returns>The rows.</returns>
            private static List<string> ReadRows(string path)
            {
                var rows = new List<string>();
                foreach (var line in File.ReadLines(path, Encoding.UTF8))
                {
                    if (line.Length > 0 && line[0] != Comment)
                    {
                        rows.Add(line);
                    }
                }

                return rows;
            }

            /// <summary>Picks the value of a row from the first column that has a BMP value.</summary>
            /// <param name="fields">The row's fields.</param>
            /// <param name="columns">The Unicode column indexes, in order of preference.</param>
            /// <returns>The value, or zero.</returns>
            private static char Pick(string[] fields, int[] columns)
            {
                foreach (var column in columns)
                {
                    var value = PickCell(fields[column]);
                    if (value != 0)
                    {
                        return value;
                    }
                }

                return '\0';
            }

            /// <summary>
            /// Picks the value of a cell. Horizontal codes come before vertical-only ones, and an ordinary character before a
            /// radical, presentation or private-use form; among equals the first listed wins.
            /// </summary>
            /// <param name="cell">The cell text.</param>
            /// <returns>The value, or zero.</returns>
            private static char PickCell(string cell)
            {
                if (cell == Unused)
                {
                    return '\0';
                }

                var horizontal = Candidates(cell, false);
                return Best(horizontal.Count > 0 ? horizontal : Candidates(cell, true));
            }

            /// <summary>Lists the BMP characters of a cell.</summary>
            /// <param name="cell">The cell text.</param>
            /// <param name="vertical">Whether to list the vertical-only codes.</param>
            /// <returns>The characters, after canonical normalization.</returns>
            private static List<char> Candidates(string cell, bool vertical)
            {
                var result = new List<char>();
                foreach (var entry in cell.Split(','))
                {
                    var code = entry.Trim();
                    var isVertical = code[^1] == VerticalSuffix;
                    var value = uint.Parse(code.TrimEnd(VerticalSuffix), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                    if (isVertical == vertical && value <= char.MaxValue && !char.IsSurrogate((char)value))
                    {
                        result.Add(Canonical((char)value));
                    }
                }

                return result;
            }

            /// <summary>Maps a compatibility ideograph to its unified ideograph when Unicode defines one.</summary>
            /// <param name="value">The character.</param>
            /// <returns>The normalized character.</returns>
            private static char Canonical(char value)
            {
                var text = value.ToString().Normalize(NormalizationForm.FormC);
                return text.Length == 1 ? text[0] : value;
            }

            /// <summary>Picks the best of some characters.</summary>
            /// <param name="candidates">The characters.</param>
            /// <returns>The first ordinary character, else the first character, else zero.</returns>
            private static char Best(List<char> candidates)
            {
                foreach (var candidate in candidates)
                {
                    if (!IsSpecialForm(candidate))
                    {
                        return candidate;
                    }
                }

                return candidates.Count > 0 ? candidates[0] : '\0';
            }

            /// <summary>Tests for a radical, vertical presentation, compatibility or private-use character.</summary>
            /// <param name="value">The character.</param>
            /// <returns><see langword="true"/> for a form that is not the character itself.</returns>
            private static bool IsSpecialForm(char value) =>
                value is >= (char)RadicalsFirst and <= (char)RadicalsLast
                    or >= (char)VerticalFormsFirst and <= (char)VerticalFormsFirstLast
                    or >= (char)CompatFormsFirst and <= (char)CompatFormsLast
                    or >= (char)PrivateFirst and <= (char)CompatIdeographsLast;
        }

        /// <summary>Writes the packed format.</summary>
        internal static class PackedTables
        {
            /// <summary>The format version of both table kinds.</summary>
            private const byte Version = 1;

            /// <summary>The flag of a vertical CMap.</summary>
            private const byte VerticalFlag = 1;

            /// <summary>The bits each LEB128 byte carries.</summary>
            private const int GroupBits = 7;

            /// <summary>The value bits of a LEB128 byte.</summary>
            private const uint GroupMask = 0x7F;

            /// <summary>The flag of a LEB128 byte that another byte follows.</summary>
            private const uint MoreFlag = 0x80;

            /// <summary>The shift that spreads the sign of a 32-bit value.</summary>
            private const int SignShift = 31;

            /// <summary>The coding value of a national encoding.</summary>
            private const byte Native = 4;

            /// <summary>The coding value of UCS-2 CMaps.</summary>
            private const byte Ucs2 = 2;

            /// <summary>The coding value of UTF-16 CMaps.</summary>
            private const byte Utf16 = 3;

            /// <summary>The coding value of UTF-32 CMaps.</summary>
            private const byte Utf32 = 5;

            /// <summary>Compresses a packed table and writes it.</summary>
            /// <param name="path">The output file.</param>
            /// <param name="packed">The packed bytes.</param>
            internal static void Write(string path, byte[] packed)
            {
                using var file = File.Create(path);
                using var zlib = new ZLibStream(file, CompressionLevel.SmallestSize);
                zlib.Write(packed);
            }

            /// <summary>Packs a CID-to-Unicode table.</summary>
            /// <param name="values">The value of each CID.</param>
            /// <returns>The packed bytes.</returns>
            internal static byte[] PackUnicode(char[] values)
            {
                var output = new MemoryStream();
                output.WriteByte(Version);
                WriteNumber(output, (uint)values.Length);
                var previous = 0;
                foreach (var value in values)
                {
                    WriteSigned(output, value - previous);
                    previous = value;
                }

                return output.ToArray();
            }

            /// <summary>Packs a CMap.</summary>
            /// <param name="maps">Every parsed CMap of the collection.</param>
            /// <param name="name">The CMap to pack.</param>
            /// <param name="collection">The collection it belongs to.</param>
            /// <returns>The packed bytes.</returns>
            internal static byte[] PackCMap(Dictionary<string, AdobeCMap> maps, string name, Collection collection)
            {
                var map = maps[name];
                var output = new MemoryStream();
                output.WriteByte(Version);
                output.WriteByte(map.Vertical ? VerticalFlag : (byte)0);
                output.WriteByte(Coding(name));
                output.WriteByte(collection.Script);
                var parent = Encoding.ASCII.GetBytes(map.Parent);
                output.WriteByte((byte)parent.Length);
                output.Write(parent);
                var codespaces = new List<Codespace>();
                AddCodespaces(maps, name, codespaces);
                codespaces.Sort(static (a, b) => a.Length.CompareTo(b.Length));
                output.WriteByte((byte)codespaces.Count);
                foreach (var codespace in codespaces)
                {
                    output.WriteByte((byte)codespace.Length);
                    WriteNumber(output, codespace.Low);
                    WriteNumber(output, codespace.High);
                }

                WriteRanges(output, Merge(Sort(map.Ranges)));
                return output.ToArray();
            }

            /// <summary>Collects the codespace ranges of a CMap and the CMaps it builds on, since the packed reader does not inherit them.</summary>
            /// <param name="maps">Every parsed CMap.</param>
            /// <param name="name">The CMap name.</param>
            /// <param name="output">Receives the ranges without duplicates.</param>
            private static void AddCodespaces(Dictionary<string, AdobeCMap> maps, string name, List<Codespace> output)
            {
                var map = maps[name];
                if (map.Parent.Length > 0)
                {
                    AddCodespaces(maps, map.Parent, output);
                }

                foreach (var codespace in map.Codespaces)
                {
                    if (!output.Contains(codespace))
                    {
                        output.Add(codespace);
                    }
                }
            }

            /// <summary>Sorts ranges by first code, keeping file order among equal codes so the later entry wins.</summary>
            /// <param name="ranges">The ranges.</param>
            /// <returns>The sorted ranges.</returns>
            private static List<CidRange> Sort(List<CidRange> ranges)
            {
                var sorted = new List<CidRange>(ranges);
                var order = new int[sorted.Count];
                for (var i = 0; i < order.Length; i++)
                {
                    order[i] = i;
                }

                Array.Sort(order, (a, b) => sorted[a].Low != sorted[b].Low ? sorted[a].Low.CompareTo(sorted[b].Low) : a.CompareTo(b));
                var result = new List<CidRange>(sorted.Count);
                foreach (var index in order)
                {
                    result.Add(sorted[index]);
                }

                return result;
            }

            /// <summary>Joins neighbouring ranges that continue each other.</summary>
            /// <param name="ranges">The sorted ranges.</param>
            /// <returns>The merged ranges.</returns>
            private static List<CidRange> Merge(List<CidRange> ranges)
            {
                var merged = new List<CidRange>(ranges.Count);
                foreach (var range in ranges)
                {
                    if (merged.Count > 0 && Continues(merged[^1], range))
                    {
                        merged[^1] = merged[^1] with { High = range.High };
                    }
                    else
                    {
                        merged.Add(range);
                    }
                }

                return merged;
            }

            /// <summary>Tests whether a range directly continues another.</summary>
            /// <param name="previous">The earlier range.</param>
            /// <param name="next">The later range.</param>
            /// <returns><see langword="true"/> when joining them changes no lookup.</returns>
            private static bool Continues(CidRange previous, CidRange next) =>
                previous.High != uint.MaxValue && next.Low == previous.High + 1 && next.Cid == previous.Cid + (int)(previous.High - previous.Low) + 1;

            /// <summary>Writes the CID ranges as deltas.</summary>
            /// <param name="output">The output.</param>
            /// <param name="ranges">The sorted ranges.</param>
            private static void WriteRanges(MemoryStream output, List<CidRange> ranges)
            {
                WriteNumber(output, (uint)ranges.Count);
                uint low = 0;
                var nextCid = 0;
                foreach (var range in ranges)
                {
                    WriteNumber(output, range.Low - low);
                    WriteNumber(output, range.High - range.Low);
                    WriteSigned(output, range.Cid - nextCid);
                    low = range.Low;
                    nextCid = range.Cid + (int)(range.High - range.Low) + 1;
                }
            }

            /// <summary>Gets the coding of a CMap from its name.</summary>
            /// <param name="name">The CMap name.</param>
            /// <returns>The <c>CidCoding</c> value.</returns>
            private static byte Coding(string name)
            {
                if (name.Contains("UCS2", StringComparison.Ordinal))
                {
                    return Ucs2;
                }

                if (name.Contains("UTF16", StringComparison.Ordinal))
                {
                    return Utf16;
                }

                return name.Contains("UTF32", StringComparison.Ordinal) ? Utf32 : Native;
            }

            /// <summary>Writes an unsigned LEB128 number.</summary>
            /// <param name="output">The output.</param>
            /// <param name="value">The number.</param>
            private static void WriteNumber(MemoryStream output, uint value)
            {
                while (value > GroupMask)
                {
                    output.WriteByte((byte)((value & GroupMask) | MoreFlag));
                    value >>= GroupBits;
                }

                output.WriteByte((byte)value);
            }

            /// <summary>Writes a zigzag-encoded signed number.</summary>
            /// <param name="output">The output.</param>
            /// <param name="value">The number.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void WriteSigned(MemoryStream output, int value) =>
                WriteNumber(output, (uint)((value << 1) ^ (value >> SignShift)));
        }

        /// <summary>Reads packed tables back.</summary>
        internal static class TableReader
        {
            /// <summary>The position of the parent name length in a packed CMap.</summary>
            private const int ParentLengthAt = 4;

            /// <summary>The bits each LEB128 byte carries.</summary>
            private const int GroupBits = 7;

            /// <summary>The value bits of a LEB128 byte.</summary>
            private const int GroupMask = 0x7F;

            /// <summary>The flag of a LEB128 byte that another byte follows.</summary>
            private const int MoreFlag = 0x80;

            /// <summary>The suffix of the CID-to-Unicode tables.</summary>
            private const string UnicodeSuffix = "-UCS2";

            /// <summary>The prefix of the CID-to-Unicode tables.</summary>
            private const string UnicodePrefix = "Adobe-";

            /// <summary>Decodes a file.</summary>
            /// <param name="folder">The folder.</param>
            /// <param name="name">The table name.</param>
            /// <returns>The table.</returns>
            internal static DecodedTable Read(string folder, string name)
            {
                using var file = File.OpenRead(Path.Combine(folder, $"{name}.bin"));
                using var zlib = new ZLibStream(file, CompressionMode.Decompress);
                var data = new MemoryStream();
                zlib.CopyTo(data);
                var bytes = data.ToArray();
                return name.EndsWith(UnicodeSuffix, StringComparison.Ordinal) && name.StartsWith(UnicodePrefix, StringComparison.Ordinal)
                    ? ReadUnicode(bytes)
                    : ReadCMap(folder, bytes);
            }

            /// <summary>Decodes a CID-to-Unicode table.</summary>
            /// <param name="bytes">The decompressed bytes.</param>
            /// <returns>The table.</returns>
            private static DecodedTable ReadUnicode(byte[] bytes)
            {
                var position = 1;
                var count = (int)Number(bytes, ref position);
                var values = new char[count];
                var previous = 0;
                for (var i = 0; i < count; i++)
                {
                    previous += Signed(bytes, ref position);
                    values[i] = (char)previous;
                }

                return new($"version {bytes[0]}", string.Empty, [], values);
            }

            /// <summary>Decodes a CMap and merges in its parents.</summary>
            /// <param name="folder">The folder, for the parents.</param>
            /// <param name="bytes">The decompressed bytes.</param>
            /// <returns>The table.</returns>
            private static DecodedTable ReadCMap(string folder, byte[] bytes)
            {
                int parentLength = bytes[ParentLengthAt];
                var parent = Encoding.ASCII.GetString(bytes, ParentLengthAt + 1, parentLength);
                var header = $"version {bytes[0]} flags {bytes[1]} coding {bytes[2]} collection {bytes[3]} parent '{parent}'";
                var position = ParentLengthAt + 1 + parentLength;
                var codespaces = ReadCodespaces(bytes, ref position);
                var cids = parent.Length == 0 ? [] : new Dictionary<uint, int>(Read(folder, parent).Cids);
                ReadRanges(bytes, position, cids);
                return new(header, codespaces, cids, []);
            }

            /// <summary>Reads the codespace ranges as text.</summary>
            /// <param name="bytes">The bytes.</param>
            /// <param name="position">The position of the count, advanced past the ranges.</param>
            /// <returns>The ranges as text.</returns>
            private static string ReadCodespaces(byte[] bytes, ref int position)
            {
                var text = new StringBuilder();
                var count = bytes[position];
                position++;
                for (var i = 0; i < count; i++)
                {
                    var length = bytes[position];
                    position++;
                    var low = Number(bytes, ref position);
                    var high = Number(bytes, ref position);
                    _ = text.Append(CultureInfo.InvariantCulture, $"{length}:{low:X}-{high:X} ");
                }

                return text.ToString();
            }

            /// <summary>Reads CID ranges into a dictionary, overriding what is there.</summary>
            /// <param name="bytes">The bytes.</param>
            /// <param name="position">The position of the range count.</param>
            /// <param name="cids">The mapping to fill.</param>
            private static void ReadRanges(byte[] bytes, int position, Dictionary<uint, int> cids)
            {
                var count = (int)Number(bytes, ref position);
                uint low = 0;
                var nextCid = 0;
                for (var i = 0; i < count; i++)
                {
                    low += Number(bytes, ref position);
                    var length = Number(bytes, ref position);
                    var cid = nextCid + Signed(bytes, ref position);
                    for (uint offset = 0; offset <= length; offset++)
                    {
                        cids[low + offset] = cid + (int)offset;
                    }

                    nextCid = cid + (int)length + 1;
                }
            }

            /// <summary>Reads an unsigned LEB128 number.</summary>
            /// <param name="bytes">The bytes.</param>
            /// <param name="position">The position, advanced past the number.</param>
            /// <returns>The number.</returns>
            private static uint Number(byte[] bytes, ref int position)
            {
                uint value = 0;
                var shift = 0;
                while (true)
                {
                    var b = bytes[position];
                    position++;
                    value |= (uint)(b & GroupMask) << shift;
                    if ((b & MoreFlag) == 0)
                    {
                        return value;
                    }

                    shift += GroupBits;
                }
            }

            /// <summary>Reads a zigzag-encoded number.</summary>
            /// <param name="bytes">The bytes.</param>
            /// <param name="position">The position, advanced past the number.</param>
            /// <returns>The number.</returns>
            private static int Signed(byte[] bytes, ref int position)
            {
                var value = Number(bytes, ref position);
                return (int)(value >> 1) ^ -(int)(value & 1);
            }
        }

        /// <summary>Compares two folders of packed tables by the lookups they answer.</summary>
        internal static class TableComparer
        {
            /// <summary>The most example differences printed per table.</summary>
            private const int MaxExamples = 4;

            /// <summary>Compares the folders and prints the differences.</summary>
            /// <param name="oldFolder">The old tables.</param>
            /// <param name="newFolder">The new tables.</param>
            /// <returns><see langword="true"/> when nothing present in the old tables is missing or changed in the new ones.</returns>
            internal static bool Compare(string oldFolder, string newFolder)
            {
                var clean = true;
                var oldCount = Directory.GetFiles(oldFolder, "*.bin").Length;
                var newCount = Directory.GetFiles(newFolder, "*.bin").Length;
                Console.WriteLine($"files: old {oldCount}, new {newCount}");
                foreach (var collection in Collections.All)
                {
                    foreach (var name in collection.CMaps)
                    {
                        clean &= CompareOne(oldFolder, newFolder, name);
                    }

                    clean &= CompareOne(oldFolder, newFolder, collection.UnicodeTable);
                }

                return clean;
            }

            /// <summary>Compares one table.</summary>
            /// <param name="oldFolder">The old tables.</param>
            /// <param name="newFolder">The new tables.</param>
            /// <param name="name">The table name.</param>
            /// <returns><see langword="true"/> when nothing is lost or changed.</returns>
            private static bool CompareOne(string oldFolder, string newFolder, string name)
            {
                var old = TableReader.Read(oldFolder, name);
                var created = TableReader.Read(newFolder, name);
                var counts = old.Unicode.Length > 0 || created.Unicode.Length > 0
                    ? CountUnicode(old.Unicode, created.Unicode)
                    : CountCids(old.Cids, created.Cids);
                var notes = old.Codespaces == created.Codespaces ? string.Empty : $" codespaces [{old.Codespaces}] -> [{created.Codespaces}]";
                Console.WriteLine($"{name}: same {counts.Same}, added {counts.Added}, lost {counts.Lost}, changed {counts.Changed}{notes}");
                return counts.Lost == 0 && counts.Changed == 0;
            }

            /// <summary>Counts the differences between two code-to-CID mappings.</summary>
            /// <param name="old">The old mapping.</param>
            /// <param name="created">The new mapping.</param>
            /// <returns>The counts.</returns>
            private static Counts CountCids(Dictionary<uint, int> old, Dictionary<uint, int> created)
            {
                var counts = new Counts(0, 0, 0, 0);
                var examples = 0;
                foreach (var (code, cid) in old)
                {
                    var found = created.TryGetValue(code, out var other);
                    var outcome = Classify(found, cid, other);
                    counts = Tally(counts, outcome);
                    examples += Example(outcome, examples, code, cid, found ? other : -1);
                }

                foreach (var code in created.Keys)
                {
                    counts = old.ContainsKey(code) ? counts : Tally(counts, Outcome.Added);
                }

                return counts;
            }

            /// <summary>Prints a difference while fewer than the maximum examples are shown.</summary>
            /// <param name="outcome">The outcome of the code.</param>
            /// <param name="shown">The examples shown so far.</param>
            /// <param name="code">The code.</param>
            /// <param name="old">The old CID.</param>
            /// <param name="created">The new CID, or -1.</param>
            /// <returns>1 when an example was printed, otherwise 0.</returns>
            private static int Example(Outcome outcome, int shown, uint code, int old, int created)
            {
                if (outcome == Outcome.Same || shown >= MaxExamples)
                {
                    return 0;
                }

                Console.WriteLine($"  code {code:X}: old cid {old}, new {created}");
                return 1;
            }

            /// <summary>Counts the differences between two CID-to-Unicode tables.</summary>
            /// <param name="old">The old values.</param>
            /// <param name="created">The new values.</param>
            /// <returns>The counts.</returns>
            private static Counts CountUnicode(char[] old, char[] created)
            {
                var counts = new Counts(0, 0, 0, 0);
                for (var cid = 0; cid < Math.Max(old.Length, created.Length); cid++)
                {
                    var a = cid < old.Length ? old[cid] : '\0';
                    var b = cid < created.Length ? created[cid] : '\0';
                    counts = Tally(counts, Classify(a, b));
                }

                return counts;
            }

            /// <summary>Classifies an old CID against the new mapping.</summary>
            /// <param name="found">Whether the new mapping holds the code.</param>
            /// <param name="old">The old CID.</param>
            /// <param name="created">The new CID.</param>
            /// <returns>The outcome.</returns>
            private static Outcome Classify(bool found, int old, int created)
            {
                if (!found)
                {
                    return Outcome.Lost;
                }

                return old == created ? Outcome.Same : Outcome.Changed;
            }

            /// <summary>Classifies a pair of values.</summary>
            /// <param name="old">The old value.</param>
            /// <param name="created">The new value.</param>
            /// <returns>The outcome; <see cref="Outcome.None"/> when neither has a value.</returns>
            private static Outcome Classify(char old, char created)
            {
                if (old == created)
                {
                    return old == '\0' ? Outcome.None : Outcome.Same;
                }

                if (old == '\0')
                {
                    return Outcome.Added;
                }

                return created == '\0' ? Outcome.Lost : Outcome.Changed;
            }

            /// <summary>Adds an outcome to the counts.</summary>
            /// <param name="counts">The counts.</param>
            /// <param name="outcome">The outcome.</param>
            /// <returns>The new counts.</returns>
            private static Counts Tally(Counts counts, Outcome outcome) => outcome switch
            {
                Outcome.Same => counts with { Same = counts.Same + 1 },
                Outcome.Added => counts with { Added = counts.Added + 1 },
                Outcome.Lost => counts with { Lost = counts.Lost + 1 },
                Outcome.Changed => counts with { Changed = counts.Changed + 1 },
                _ => counts,
            };
        }

        /// <summary>One Adobe character collection and the tables built for it.</summary>
        /// <param name="Folder">The folder of the collection in cmap-resources.</param>
        /// <param name="Script">The <c>CjkScript</c> value stored in the packed CMaps.</param>
        /// <param name="UnicodeTable">The name of the packed CID-to-Unicode table.</param>
        /// <param name="UnicodeColumns">The Unicode columns of cid2code.txt, in order of preference.</param>
        /// <param name="CMaps">The names of the CMaps packed for the collection.</param>
        internal sealed record Collection(string Folder, byte Script, string UnicodeTable, string[] UnicodeColumns, string[] CMaps);

        /// <summary>A parsed CMap file.</summary>
        /// <param name="Parent">The name of the CMap it builds on, or an empty string.</param>
        /// <param name="Vertical">Whether the writing mode is vertical.</param>
        /// <param name="Codespaces">The codespace ranges in file order.</param>
        /// <param name="Ranges">The CID ranges in file order.</param>
        internal sealed record AdobeCMap(string Parent, bool Vertical, List<Codespace> Codespaces, List<CidRange> Ranges);

        /// <summary>A packed CMap or table decoded for comparison.</summary>
        /// <param name="Header">The version, flags, coding, collection and parent name as text.</param>
        /// <param name="Codespaces">The codespace ranges as text.</param>
        /// <param name="Cids">The effective code-to-CID mapping, base CMap ranges included.</param>
        /// <param name="Unicode">The CID-to-Unicode values of a table.</param>
        internal sealed record DecodedTable(string Header, string Codespaces, Dictionary<uint, int> Cids, char[] Unicode);
    }
}
