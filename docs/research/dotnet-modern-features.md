# Modern .NET features for HyperPdfLibrary

HyperPdfLibrary targets `net10.0;net11.0`. Shared code can use every .NET 10 and C# 14 feature with no guard. .NET 11 APIs need `#if NET11_0_OR_GREATER` and a .NET 10 fallback. C# 15 syntax cannot appear in shared code while `net10.0` builds with C# 14.

The biggest wins are:

1. .NET 11 span-based `ZLibDecoder` for the Flate filter. It needs no `Stream` and reports truncated or corrupt data as a status, not an exception.
2. `SearchValues<byte>` (.NET 8) for the lexer.
3. Dictionary alternate lookup (.NET 9) to intern names from raw bytes without allocating.
4. `Vector128` for predictors and pixel conversion. .NET 11 guarantees SSE4.2 on x64, so `Vector128` paths always run in hardware there.
5. Hand-written union structs, not the C# 15 `union` keyword, for the PDF object model. Generated unions box value cases.

Every choice below still needs a BenchmarkDotNet and EventPipe measurement before it ships. This note says where to look, not what is fastest.

## How to read this note

| Column | Meaning |
|---|---|
| Ver | First .NET or C# version with the feature |
| Guard | `none` works on both targets. `NET11` needs `#if NET11_0_OR_GREATER` and a .NET 10 fallback |
| Guard `C# 15` | Needs C# 15. The `net10.0` build defaults to C# 14, so shared code must avoid it or raise `LangVersion` for both targets, which is unsupported on `net10.0` |
| Source | Numbered link in [Sources](#sources) |

API shapes for .NET 11 were checked against the reference pack `Microsoft.NETCore.App.Ref 11.0.0-rc.1.26425.128` with `System.Reflection.Metadata`. Behaviour notes marked "probe" come from a small file-based app run on .NET 11.0.0 RC1 on Linux x64.

## Lexer and tokens

| Ver | API | Replaces | HyperPdf use | Guard | Source |
|---|---|---|---|---|---|
| .NET 8 | `SearchValues.Create(ReadOnlySpan<byte>)` with `IndexOfAny`, `IndexOfAnyExcept` | Hand loops or `IndexOfAny(a, b, c)` with at most 5 values | Find the end of a token with one set: the 6 PDF white-space bytes plus the 10 delimiters `( ) < > [ ] { } / %`. Skip white space with `IndexOfAnyExcept(whitespace)` | none | [2] |
| .NET 8 | `ReadOnlySpan<byte>.IndexOf("endstream"u8)`, `LastIndexOf("startxref"u8)` | Byte-by-byte search | Stream recovery when `/Length` is wrong; trailer search from the end | none | [2] |
| .NET 10 | `MemoryExtensions.CountAny(span, SearchValues<T>)` | Counting loops | Count line ends for diagnostics | none | [7] |
| .NET 11 | `MemoryExtensions.Min` / `Max` on spans | LINQ or loops | Bounds of `/Widths` and `/Decode` arrays | NET11 | [13] |
| C# 15 | Labeled `break` and `continue` | `goto` or flag variables | Leave nested loops in the cross-reference repair scan | C# 15 | [21] |

`SearchValues<string>` (.NET 9) searches `char` text only. It does not help a byte lexer. Use one `SearchValues<byte>` per byte class and keep it in a `static readonly` field.

## Numbers

| Ver | API | Behaviour (probe) | HyperPdf use | Guard | Source |
|---|---|---|---|---|---|
| Core 2.1 | `Utf8Parser.TryParse(span, out int, out consumed)` | Stops at the first bad byte and reports bytes consumed. Accepts `+17`. Parses `1e5` as `1` | Fast integer path: parse at the token start, check `consumed` equals the token length | none | [25] |
| Core 2.1 | `Utf8Parser.TryParse(span, out double, out consumed)` | Accepts `-.002`, `.5`, `4.`. Also accepts `1e5` as 100000, which PDF forbids | Avoid for reals unless you reject `e` and `E` first | none | [25] |
| .NET 8 | `IUtf8SpanParsable<T>`: `double.TryParse(ReadOnlySpan<byte>, NumberStyles, IFormatProvider, out double)` | Needs the whole token. With `AllowLeadingSign \| AllowDecimalPoint` it accepts `-.002`, `.5`, `4.` and rejects `1e5` | Real numbers after the lexer has found the token end | none | [14] |
| .NET 8 | `IUtf8SpanFormattable`, `Utf8.TryWrite` | Formats into `Span<byte>` | Write numbers in the annotation writer and appearance streams without strings | none | [2] |

Most content-stream numbers are small integers. A hand-written digit loop for integers, with a fall back to `double.TryParse` for reals, is the likely best mix. Benchmark it against `Utf8Parser` on a real content stream.

## Strings, hex and text

| Ver | API | HyperPdf use | Guard | Source |
|---|---|---|---|---|
| .NET 10 | `Convert.FromHexString(ReadOnlySpan<byte> utf8, Span<byte>, out consumed, out written)` returning `OperationStatus` | Fast path for hex strings `<...>` and `ASCIIHexDecode` runs with no white space. PDF allows white space and an odd final digit, so keep a fallback loop | none | [7] |
| .NET 8 | `Utf8.IsValid`, `Ascii.IsValid` | Check PDF 2.0 UTF-8 text strings (BOM `EF BB BF`) before decoding | none | [2] |
| .NET 11 | `Utf8.IndexOfInvalidSubsequence`, `Utf16.IsValid`, `Utf16.IndexOfInvalidSubsequence` | Report the exact bad byte in a text string, then fall back to PDFDocEncoding. UTF-16BE strings need a byte swap before `Utf16` checks | NET11 | [13] |
| .NET 11 | `RunePosition.EnumerateUtf8` | Walk extracted text by code point for search and copy | NET11 | [13] |

## Filters and streams

| Ver | API | Replaces | HyperPdf use | Guard | Source |
|---|---|---|---|---|---|
| .NET 9 | zlib-ng inside `System.IO.Compression` | Classic zlib | Faster Flate on every target with no code change | none | [5] |
| .NET 9 | `ZLibCompressionOptions` (level and strategy) | `CompressionLevel` only | Tune compression when the writer saves streams | none | [5] |
| .NET 11 | `ZLibDecoder`, `DeflateDecoder`, `GZipDecoder` | `ZLibStream` over a `MemoryStream` | `FlateDecode`. Span in, span out, no `Stream` | NET11 | [11], [12] |
| .NET 11 | `ZLibEncoder`, `DeflateEncoder` with `windowLog2`, `ZLibCompressionOptions.WindowLog2` | `ZLibStream` writes | Writer output for appearance streams and incremental saves | NET11 | [11], [12] |
| .NET 11 | `ReadOnlyMemoryStream`, `WritableMemoryStream`, `ReadOnlySequenceStream` | `new MemoryStream(array, offset, count, false)` | Hand pooled or mapped `ReadOnlyMemory<byte>` to APIs that demand a `Stream`, such as SkiaSharp codecs for `DCTDecode` | NET11 | [11] |

### Exact .NET 11 decoder shape

All six types are `sealed class` and `IDisposable`. They are not structs. Reuse one instance per document or per thread and call `Reset()` between streams.

```csharp
public sealed class ZLibDecoder : IDisposable
{
    public ZLibDecoder();
    public OperationStatus Decompress(ReadOnlySpan<byte> source, Span<byte> destination,
        out int bytesConsumed, out int bytesWritten);
    public static bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten);
    public void Reset();
    public void Dispose();
}
```

Probe results on .NET 11 RC1:

| Input | Result |
|---|---|
| Valid data, big enough buffer | `TryDecompress` returns `true` |
| Destination too small | `TryDecompress` returns `false` and writes 0. `Decompress` returns `DestinationTooSmall` with partial output |
| Truncated data | `Decompress` returns `NeedMoreData` and keeps the bytes it could decode |
| Corrupt data | `Decompress` returns `InvalidData`. No exception |
| Raw deflate with no zlib header | `DeflateDecoder.TryDecompress` succeeds |

What this means for HyperPdf:

- PDF `/Length` gives the compressed size, not the decoded size. Use the instance `Decompress` loop and grow a pooled buffer on `DestinationTooSmall`. `TryDecompress` throws away its work when the buffer is short.
- `NeedMoreData` with partial output gives repair mode the decoded prefix of a truncated stream. Many damaged PDFs need this.
- `InvalidData` as a status keeps exceptions off the render path.
- Fall back to `DeflateDecoder` when the two-byte zlib header is missing or wrong.
- Whether `ZLibDecoder` rejects a bad Adler-32 trailer was not tested. Test it with a fixture before relying on it.
- The .NET 10 fallback is `ZLibStream` over a small read-only stream wrapper. Measure its allocations against the .NET 11 path.

Zstandard (`ZstandardDecoder`, .NET 11) has no PDF filter. Do not add it to the reader.

## Predictors and pixels

| Ver | API | HyperPdf use | Guard | Source |
|---|---|---|---|---|
| .NET 7 | `Vector128<byte>` operators, `Vector128.Shuffle`, `Narrow`, `Widen`, `LoadUnsafe` | PNG `Up` predictor is a wrapping byte add of two rows. RGB to BGRA and gray to BGRA expansion for SkiaSharp | none | [9] |
| .NET 8 | `Vector512<T>` | Wide paths on AVX-512 machines only. Keep `Vector128` as the main path | none | [2] |
| .NET 8 | Better hardware acceleration for `Matrix3x2` and `Vector256` | CTM and text matrix maths in `float`, which matches SkiaSharp. Check precision on large page coordinates | none | [2] |
| .NET 10 | `Vector128.AddSaturate`, `Count`, `IndexOf` | Clamp CMYK sums; count set mask bytes | none | [7] |
| .NET 11 | `Vector128.Unzip`, `UnzipEven`, `UnzipOdd`, `Zip`, `Concat*`, `Reverse`, `CreateAlternatingSequence` | Split interleaved samples into planes and join them back. Use `Shuffle` with constant indices on .NET 10 | NET11 | [10], [13] |
| .NET 11 | Hardware baseline x86-64-v2 (SSSE3, SSE4.1, SSE4.2) | `Vector128.Shuffle` on bytes maps to `pshufb` on every supported x64 CPU. Linux ReadyToRun targets x86-64-v3 | none | [10] |

PNG `Sub`, `Average` and `Paeth` depend on the previous pixel. They vectorise only across the bytes of one pixel. Benchmark a scalar loop first. TIFF predictor 2 has the same chain.

## Decryption

| Ver | API | HyperPdf use | Guard | Source |
|---|---|---|---|---|
| .NET 5 | `MD5.HashData(ReadOnlySpan<byte>, Span<byte>)` | Standard security handler key (R2 to R4) and per-object keys | none | [18] |
| .NET 5 | `SHA256.HashData`, `SHA384.HashData`, `SHA512.HashData` with span destination | R6 hash (Algorithm 2.B) | none | [18] |
| .NET 6 | `Aes.DecryptCbc(ReadOnlySpan<byte>, ReadOnlySpan<byte> iv, Span<byte>, PaddingMode)` | AESV2 and AESV3 strings and streams. The first 16 bytes are the IV. Use `PKCS7` for content | none | [17] |
| .NET 6 | `Aes.EncryptCbc(..., PaddingMode.None)`, `Aes.DecryptEcb` | R6 Algorithm 2.B rounds use AES-128-CBC with no padding. `/Perms` check uses AES-256-ECB | none | [17] |
| .NET 9 | `CryptographicOperations.HashData(HashAlgorithmName, ...)` | Pick SHA-256, 384 or 512 by name inside the R6 loop | none | [6] |
| .NET 11 | `CryptographicOperations.FixedTimeEquals(ReadOnlySpan<byte>, byte)` | Not needed for reading. Password checks are not secret comparisons | NET11 | [13] |

Notes:

- RC4 is not in the base library. Write it in managed code. It is about 30 lines.
- AESV2 derives a new key per object. Setting `Aes.Key` copies the key into a new array each time. Measure this. Keep one `Aes` per document.
- On Linux these APIs call OpenSSL. A system in FIPS mode can refuse MD5. Report that as a clear error, not a crash.

## Collections and lookup

| Ver | API | HyperPdf use | Guard | Source |
|---|---|---|---|---|
| .NET 9 | `Dictionary<TKey,TValue>.GetAlternateLookup<TAlternate>()` and `HashSet<T>` equivalent | Name interning. Give `PdfName` a comparer that implements `IAlternateEqualityComparer<ReadOnlySpan<byte>, PdfName>`. The lexer then looks up raw bytes and creates a `PdfName` only on a miss | none | [4] |
| .NET 9 | `FrozenDictionary<TKey,TValue>.GetAlternateLookup` | Fixed tables built once: operator names, standard 14 font names, filter names | none | [4] |
| .NET 8 | `FrozenDictionary`, `FrozenSet` | Same fixed tables. Build cost is high, so use them only for static tables | none | [2] |
| .NET 9 | `OrderedDictionary<TKey,TValue>` | Keep key order when the writer re-saves a dictionary, so diffs stay small | none | [4] |
| .NET 10 | `OrderedDictionary.TryAdd` and `TryGetValue` with index | Update a key in place during writes | none | [7] |
| .NET 9 | `System.Threading.Lock` | Guard the shared object cache. The `lock` statement uses it directly | none | [19] |

Built-in string comparers offer alternate lookup for `ReadOnlySpan<char>` only. Byte spans need the custom comparer.

## Language features

| Ver | Feature | HyperPdf use | Guard | Source |
|---|---|---|---|---|
| C# 11 | `"..."u8` literals | Keyword compare: `token.SequenceEqual("obj"u8)`. Data lives in the binary | none | [3] |
| C# 12 | `[InlineArray(N)]` | Graphics-state stack (PDF 1.7 suggests 28 levels of `q`) and the operand stack (8 slots) with a pooled overflow. No per-page arrays | none | [20] |
| .NET 10 | `InlineArray2<T>` to `InlineArray16<T>` | Small fixed buffers without declaring a struct, such as a 4-entry colour | none | [15] |
| C# 12 | Collection expressions | `ReadOnlySpan<byte>` tables of constants with no allocation | none | [20] |
| C# 13 | `params ReadOnlySpan<T>` | `WriteOperator("re"u8, x, y, w, h)` in the appearance writer with no array | none | [19], [4] |
| C# 13 | `allows ref struct` and ref struct interfaces | `Parse<TSink>(ref TSink sink) where TSink : IContentSink, allows ref struct`. The JIT specialises each struct sink, and sinks may hold spans | none | [19] |
| C# 14 | `field` keyword | Lazy object-model properties: `get => field ??= Resolve();`. Less code, same cost | none | [22] |
| C# 14 | Extension members and implicit span conversions | `span.IsPdfWhitespace` style helpers. Static calls, no runtime cost | none | [22] |
| C# 15 | Collection expression arguments `[with(capacity: n), ..]` | Pre-size lists from `/Count` | C# 15 | [21] |
| C# 15 | `union` and `closed` | See below | NET11 | [21], [23] |

### Unions and boxing

The compiler turns `union X(A, B)` into a struct with one `object? Value` property. Learn says it "always boxes value-type cases" [23]. A `union PdfObject(long, double, bool, PdfName, ...)` would allocate for every number. Do not use it.

Use a hand-written struct instead. Mark it `[Union]` and implement the non-boxing access pattern: `HasValue` plus one `TryGetValue(out T)` per case. The compiler then calls `TryGetValue` for type patterns and does not box [23]. Store a kind tag, an 8-byte payload for numbers and booleans, and one object reference for names, strings, arrays and dictionaries.

`UnionAttribute`, `IUnion` and `IsClosedTypeAttribute` exist only in .NET 11. Put the attribute and interface behind `#if NET11_0_OR_GREATER`. The struct layout works on .NET 10 without them. You lose only exhaustive `switch` checks on that target. Learn still lists C# 15 as a preview, and parts of the union proposal are not yet built [21].

`closed` classes give exhaustive switches over reference types. They suit annotation kinds and XObject kinds, which are already classes.

## Runtime and JIT gains you get for free

| Ver | Change | Effect on HyperPdf | Source |
|---|---|---|---|
| .NET 8 | Dynamic PGO on by default | Hot lexer and operator loops get tier-1 code after warm-up | [1] |
| .NET 10 | Stack allocation of small fixed arrays and non-escaping delegates | Some short-lived arrays stop allocating. Do not rely on it; the allocation audit decides | [8] |
| .NET 10 | Struct argument promotion | Small structs such as `Matrix` and `PdfValue` pass in registers | [8] |
| .NET 11 | Bounds-check removal for `i + const < len` and after `!span.IsEmpty` | Lexer lookahead like `span[i + 1]` gets cheaper | [10] |
| .NET 11 | Generic virtual method devirtualisation | Helps generic sinks behind interfaces | [10] |
| .NET 11 | Faster `IndexOfAny` with `SearchValues` on Arm64 (5% to 50%) | Lexer on Apple silicon | [10] |

## Runtime async

.NET 11 adds runtime-native async. It is a preview. You turn it on per project with `<Features>runtime-async=on</Features>` [10]. It applies to `net11.0` only. The .NET libraries are already built with it.

HyperPdf parsing and rendering are CPU-bound and synchronous. Only file opening is async. Do not enable runtime async in the library yet. Revisit when it leaves preview and the app's file loading shows a measured gain.

## Native AOT

- Every API in this note is trim and AOT safe. None needs reflection.
- `SearchValues`, `FrozenDictionary` and alternate lookups are built in code. They need no runtime code generation.
- Native AOT compiles for the baseline instruction set unless the project sets `IlcInstructionSet`. Keep `Vector128` as the main path. Treat `Vector256` and `Vector512` paths as extras that must pass the same tests.
- Runtime async supports Native AOT in .NET 11 [10]. That does not change the advice above.

## Do not use

| Feature | Why |
|---|---|
| C# 15 `union` keyword for PDF values | Boxes every integer, real and boolean [23] |
| `BitArray(ReadOnlySpan<byte>)` (.NET 11) for image masks | `BitArray` puts the lowest index in the least significant bit of each byte [24]. PDF image samples start at the most significant bit. Read bits with shifts instead |
| `Utf8Parser` for reals | Accepts exponents such as `1e5`, which PDF forbids (probe) |
| `SearchValues<string>` | Searches `char` text, not bytes |
| `FrozenDictionary` for per-document data | High build cost. Use `Dictionary` for anything built per file |
| `ZLibDecoder.TryDecompress` with a guessed size | Discards output when the buffer is short. Use the `Decompress` loop |
| Zstandard APIs in the reader | No PDF filter uses Zstandard |
| Runtime async in the library | Preview, `net11.0` only, and the hot path is synchronous |
| Memory-safety preview (`unsafe` expressions, pointer relaxations) | Preview language version and feature flag. The render path should use spans |
| Interceptors | Experimental. Learn says not for production [20] |
| `Vector512` as the only fast path | Many machines lack AVX-512. AOT may not use it by default |

## Sources

1. What's new in .NET 8 runtime. <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/runtime>
2. What's new in .NET 8 runtime, core libraries sections (UTF-8, SearchValues, Frozen, Vector512). <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/runtime#core-net-libraries>
3. What's new in C# 11 (UTF-8 string literals). <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11>
4. What's new in .NET 9 libraries (alternate lookup, OrderedDictionary, params spans, Vector APIs). <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/libraries>
5. .NET 9 libraries, System.IO: zlib-ng and ZLibCompressionOptions. <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/libraries#system-io>
6. .NET 9 libraries, CryptographicOperations.HashData. <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9/libraries#cryptographicoperationshashdata-method>
7. What's new in .NET 10 libraries (UTF-8 hex conversion, OrderedDictionary). <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries>
8. What's new in .NET 10 runtime (stack allocation, struct promotion). <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime>
9. Vector128 class API reference. <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.intrinsics.vector128>
10. What's new in .NET 11 runtime (RC1: hardware baseline, runtime async, JIT, SIMD lanes). <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/runtime>
11. What's new in .NET 11 libraries (in-memory streams, span compression, Zstandard). <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries>
12. DeflateEncoder and ZLibEncoder source. <https://source.dot.net/System.IO.Compression/System/IO/Compression/DeflateEncoder.cs.html>, <https://source.dot.net/System.IO.Compression/System/IO/Compression/ZLibEncoder.cs.html>
13. .NET 11 libraries: UTF validation, BitArray, cross-lane vectors, cryptography. <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#utf-validation-and-invalid-subsequence-search>
14. IUtf8SpanParsable interface (.NET 8 and later). <https://learn.microsoft.com/en-us/dotnet/api/system.iutf8spanparsable-1>
15. Reference pack diff, .NET 10.0.12 to 11.0.0-rc.1.26425.128, read with `System.Reflection.Metadata` (local, not published).
16. Secondary write-up on .NET 11 span compression (not authoritative). <https://startdebugging.net/de/2026/05/dotnet-11-span-based-deflate-gzip-compression/>
17. SymmetricAlgorithm.DecryptCbc (.NET 6 and later). <https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.symmetricalgorithm.decryptcbc>
18. One-shot hash APIs. <https://learn.microsoft.com/en-us/dotnet/standard/security/cryptography-model#one-shot-apis>
19. What's new in C# 13. <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13>
20. What's new in C# 12. <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-12>
21. What's new in C# 15. <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15>
22. What's new in C# 14. <https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14>
23. Union types, C# reference. <https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union>
24. BitArray constructor. <https://learn.microsoft.com/en-us/dotnet/api/system.collections.bitarray.-ctor>
25. Utf8Parser.TryParse. <https://learn.microsoft.com/en-us/dotnet/api/system.buffers.text.utf8parser.tryparse>

## Limits

- Sources 3, 9, 14, 18 and 25 were not fetched for this note. They are linked from API knowledge. Check them before quoting.
- The 28-level `q` nesting figure comes from ISO 32000-1:2008 Annex C, which was not re-read for this note.
- Probe results come from one RC1 build on Linux x64. Repeat them on Windows and macOS, and on the final .NET 11 release.
- No benchmark backs any speed claim here. The project rules require one per change.
