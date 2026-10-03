// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Forms.Scripting;

namespace PdfViewerLite.Core.Tests.Forms;

/// <summary>Checks the form scripts PdfViewerLite runs itself: Acrobat's built-in functions and simplified field notation, and nothing else.</summary>
public sealed class FormScriptTests
{
    /// <summary>The position of the currency in <c>AFNumber_Format</c>.</summary>
    private const int CurrencyArgument = 4;

    /// <summary>The position of the currency-first flag.</summary>
    private const int CurrencyFirstArgument = 5;

    /// <summary>The tolerance of calculated results.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The values of the fields in the calculations.</summary>
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal) { ["Price"] = "$12.50", ["Quantity"] = "4", ["Unit Cost"] = "3", ["Empty"] = string.Empty };

    /// <summary>Acrobat's built-in calls are recognised with their arguments; anything else is not run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesBuiltInCalls()
    {
        var number = FormScript.Parse("AFNumber_Format(2, 0, 0, 0, \"$\", true);");
        var simple = FormScript.Parse("AFSimple_Calculate(\"SUM\", new Array (\"Price\", \"Quantity\"));");
        var notation = FormScript.Parse("/** BVCALC Price * Quantity EVCALC **/ event.value = AFMakeNumber(getField(\"Price\").value) * 2");
        var hostile = FormScript.Parse("app.launchURL(\"http://example.com\"); this.mailDoc();");

        await Assert.That(number.Function).IsEqualTo(FormScriptFunction.Number);
        await Assert.That(number.Text(CurrencyArgument, string.Empty)).IsEqualTo("$");
        await Assert.That(number.Flag(CurrencyFirstArgument)).IsTrue();
        await Assert.That(simple.Function).IsEqualTo(FormScriptFunction.Simple);
        await Assert.That(simple.Fields).IsEquivalentTo(["Price", "Quantity"]);
        await Assert.That(notation.Function).IsEqualTo(FormScriptFunction.Expression);
        await Assert.That(notation.Expression).IsEqualTo("Price * Quantity");
        await Assert.That(hostile.Function).IsEqualTo(FormScriptFunction.Unknown);
    }

    /// <summary>Numbers, percentages, dates and special formats are written as Acrobat writes them.</summary>
    /// <param name="script">The format script.</param>
    /// <param name="value">The value.</param>
    /// <param name="expected">The formatted value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("AFNumber_Format(2, 0, 0, 0, \"$\", true);", "1234.5", "$1,234.50")]
    [Arguments("AFNumber_Format(2, 2, 0, 0, \" €\", false);", "1234.5", "1.234,50 €")]
    [Arguments("AFNumber_Format(0, 1, 2, 0, \"\", false);", "-42", "(42)")]
    [Arguments("AFPercent_Format(1, 0);", "0.256", "25.6%")]
    [Arguments("AFDate_FormatEx(\"dd/mm/yyyy\");", "2026-03-07", "07/03/2026")]
    [Arguments("AFDate_FormatEx(\"mmm d, yyyy\");", "2026-03-07", "Mar 7, 2026")]
    [Arguments("AFSpecial_Format(2);", "0412345678", "(041) 234-5678")]
    [Arguments("AFSpecial_Format(3);", "123456789", "123-45-6789")]
    public async Task Formats(string script, string value, string expected) =>
        await Assert.That(FormScriptEngine.Format(FormScript.Parse(script), value)).IsEqualTo(expected);

    /// <summary>What is typed is checked: numbers in number fields, real dates in date fields, the right digits in special fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksWhatIsTyped()
    {
        var number = FormScript.Parse("AFNumber_Keystroke(2, 0, 0, 0, \"\", true);");
        var date = FormScript.Parse("AFDate_KeystrokeEx(\"dd/mm/yyyy\");");
        var zip = FormScript.Parse("AFSpecial_Keystroke(0);");

        await Assert.That(FormScriptEngine.Accepts(number, "$1,234.50")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(number, "twelve")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(date, "31/12/2026")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(date, "32/13/2026")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(zip, "40000")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(zip, "4000")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(FormScript.None, "anything")).IsTrue();
    }

    /// <summary>A range validation accepts values within its limits and explains a refusal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidatesRanges()
    {
        var range = FormScript.Parse("AFRange_Validate(true, 0, true, 100);");

        var inside = FormScriptEngine.Validate(range, "50", out _);
        var outside = FormScriptEngine.Validate(range, "150", out var message);

        await Assert.That(inside).IsTrue();
        await Assert.That(outside).IsFalse();
        await Assert.That(message).Contains("between 0 and 100");
    }

    /// <summary>Sums, averages, products, minimums, maximums and expressions are calculated, with formatting ignored.</summary>
    /// <param name="script">The calculate script.</param>
    /// <param name="expected">The result.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("AFSimple_Calculate(\"SUM\", new Array (\"Price\", \"Quantity\", \"Empty\"));", 16.5)]
    [Arguments("AFSimple_Calculate(\"AVG\", \"Price, Quantity\");", 8.25)]
    [Arguments("AFSimple_Calculate(\"PRD\", new Array (\"Price\", \"Quantity\"));", 50)]
    [Arguments("AFSimple_Calculate(\"MIN\", new Array (\"Price\", \"Quantity\"));", 4)]
    [Arguments("AFSimple_Calculate(\"MAX\", new Array (\"Price\", \"Quantity\"));", 12.5)]
    [Arguments("/** BVCALC (Price - 2.5) * Quantity / 2 EVCALC **/", 20)]
    [Arguments("/** BVCALC Unit\\ Cost * -Quantity EVCALC **/", -12)]
    public async Task Calculates(string script, double expected)
    {
        var calculated = FormScriptEngine.TryCalculate(FormScript.Parse(script), static name => Values.GetValueOrDefault(name), out var result);

        await Assert.That(calculated).IsTrue();
        await Assert.That(result).IsEqualTo(expected).Within(Tolerance);
    }

    /// <summary>A malformed or dividing-by-zero expression gives no result rather than a wrong one.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Price *")]
    [Arguments("(Price")]
    [Arguments("Price / Empty")]
    public async Task RefusesBadExpressions(string expression) =>
        await Assert.That(FormExpression.TryEvaluate(expression, static name => FormNumbers.TryParse(Values.GetValueOrDefault(name), out var v) ? v : 0, out _)).IsFalse();
}
