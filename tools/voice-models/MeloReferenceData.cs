// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using PdfViewerLite.Speech.Melo;

namespace PdfViewerLite.Tools.VoiceModels;

/// <summary>Checks the C# front end against the saved upstream reference.</summary>
internal static class MeloReferenceData
{
    /// <summary>The spacing between phones in a sequence with blanks.</summary>
    private const int PhoneStride = 2;

    /// <summary>Creates a native snapshot and checks it against the upstream reference.</summary>
    /// <param name="directory">The voice folder.</param>
    /// <param name="referencePath">The saved upstream reference.</param>
    /// <returns>The checked snapshot as UTF-8 JSON.</returns>
    /// <exception cref="InvalidDataException">The native snapshot differs from the reference.</exception>
    internal static byte[] Create(string directory, string referencePath)
    {
        using var reference = JsonDocument.Parse(File.ReadAllBytes(referencePath));
        var cases = reference.RootElement.GetProperty("cases");
        var predictions = reference.RootElement.GetProperty("predictions");
        if (cases.GetArrayLength() == 0 || predictions.GetArrayLength() == 0)
        {
            throw new InvalidDataException("The upstream reference must contain sentences and spelling predictions.");
        }

        var frontEnd = MeloFrontEnd.Load(directory);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            WriteCases(writer, cases, frontEnd);
            WritePredictions(writer, predictions, directory);
            writer.WriteEndObject();
        }

        var snapshot = output.ToArray();
        using var actual = JsonDocument.Parse(snapshot);
        Compare(cases, actual.RootElement.GetProperty(nameof(cases)), "text");
        Compare(predictions, actual.RootElement.GetProperty(nameof(predictions)), "word");
        Console.WriteLine($"front end: {cases.GetArrayLength()} sentences and {predictions.GetArrayLength()} spelling predictions match the upstream reference");
        return snapshot;
    }

    /// <summary>Writes native sentence inputs.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="cases">The reference sentences.</param>
    /// <param name="frontEnd">The C# front end.</param>
    /// <exception cref="InvalidDataException">A reference sentence has no text.</exception>
    private static void WriteCases(Utf8JsonWriter writer, JsonElement cases, MeloFrontEnd frontEnd)
    {
        var input = new MeloInput();
        writer.WriteStartArray(nameof(cases));
        foreach (var example in cases.EnumerateArray())
        {
            var text = example.GetProperty("text").GetString() ?? throw new InvalidDataException("A reference sentence has no text.");
            frontEnd.Prepare(text, input);
            writer.WriteStartObject();
            writer.WriteString(nameof(text), text);
            writer.WriteStartArray("phones");
            for (var index = 1; index < input.Phones.Count; index += PhoneStride)
            {
                writer.WriteStringValue(frontEnd.Symbols.Name(input.Phones[index]));
            }

            writer.WriteEndArray();
            WriteNumbers(writer, "ids", input.Phones);
            WriteNumbers(writer, "tones", input.Tones);
            WriteNumbers(writer, "languages", input.Languages);
            writer.WriteStartArray("wordToPhones");
            foreach (var count in input.PhonesPerToken)
            {
                writer.WriteNumberValue(count);
            }

            writer.WriteEndArray();
            WriteNumbers(writer, "tokenIds", input.Tokens);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes native spelling-to-sound predictions.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="predictions">The reference words.</param>
    /// <param name="directory">The voice folder.</param>
    /// <exception cref="InvalidDataException">A reference prediction has no word.</exception>
    private static void WritePredictions(Utf8JsonWriter writer, JsonElement predictions, string directory)
    {
        var spelling = new SpellingToSound(File.ReadAllBytes(Path.Combine(directory, MeloModel.SpellingFile)));
        var phones = new List<string>();
        writer.WriteStartArray(nameof(predictions));
        foreach (var prediction in predictions.EnumerateArray())
        {
            var word = prediction.GetProperty("word").GetString() ?? throw new InvalidDataException("A reference prediction has no word.");
            phones.Clear();
            spelling.Predict(word, phones);
            writer.WriteStartObject();
            writer.WriteString(nameof(word), word);
            writer.WriteStartArray(nameof(phones));
            foreach (var phone in phones)
            {
                writer.WriteStringValue(phone);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes a sequence of integer ids.</summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="numbers">The ids.</param>
    private static void WriteNumbers(Utf8JsonWriter writer, string name, List<long> numbers)
    {
        writer.WriteStartArray(name);
        foreach (var number in numbers)
        {
            writer.WriteNumberValue(number);
        }

        writer.WriteEndArray();
    }

    /// <summary>Reports the first reference field that differs.</summary>
    /// <param name="expected">The upstream cases.</param>
    /// <param name="actual">The native cases.</param>
    /// <param name="label">The field that identifies a case.</param>
    /// <exception cref="InvalidDataException">A field differs.</exception>
    private static void Compare(JsonElement expected, JsonElement actual, string label)
    {
        for (var index = 0; index < expected.GetArrayLength(); index++)
        {
            foreach (var property in expected[index].EnumerateObject())
            {
                if (!JsonElement.DeepEquals(property.Value, actual[index].GetProperty(property.Name)))
                {
                    throw new InvalidDataException($"{expected[index].GetProperty(label).GetString()}: {property.Name} differs from the upstream reference.");
                }
            }
        }
    }
}
