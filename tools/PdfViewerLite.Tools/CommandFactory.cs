// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.CommandLine;

namespace PdfViewerLite.Tools;

/// <summary>Creates commands with named positional arguments.</summary>
internal static class CommandFactory
{
    /// <summary>Creates a synchronous command.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">The help description.</param>
    /// <param name="handler">The command implementation.</param>
    /// <param name="names">Argument names; a trailing question mark makes an argument optional.</param>
    /// <returns>The command.</returns>
    internal static Command Create(string name, string description, Func<string[], int> handler, params string[] names)
    {
        var (command, arguments) = CreateArguments(name, description, names);
        command.SetAction(result => handler(ReadArguments(result, arguments)));
        return command;
    }

    /// <summary>Creates an asynchronous command.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">The help description.</param>
    /// <param name="handler">The command implementation.</param>
    /// <param name="names">Argument names; a trailing question mark makes an argument optional.</param>
    /// <returns>The command.</returns>
    internal static Command Create(string name, string description, Func<string[], Task<int>> handler, params string[] names)
    {
        var (command, arguments) = CreateArguments(name, description, names);
        command.SetAction((result, _) => handler(ReadArguments(result, arguments)));
        return command;
    }

    /// <summary>Creates the positional argument definitions.</summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">The help description.</param>
    /// <param name="names">The argument names.</param>
    /// <returns>The command and its arguments.</returns>
    private static (Command Command, List<Argument<string>> Arguments) CreateArguments(string name, string description, string[] names)
    {
        var command = new Command(name, description);
        var arguments = new List<Argument<string>>(names.Length);
        foreach (var argumentName in names)
        {
            var optional = argumentName.EndsWith('?');
            var argument = new Argument<string>(optional ? argumentName[..^1] : argumentName) { Arity = optional ? ArgumentArity.ZeroOrOne : ArgumentArity.ExactlyOne, };
            arguments.Add(argument);
            command.Arguments.Add(argument);
        }

        return (command, arguments);
    }

    /// <summary>Reads supplied argument values in declaration order.</summary>
    /// <param name="result">The parsed command.</param>
    /// <param name="arguments">The argument definitions.</param>
    /// <returns>The supplied values.</returns>
    private static string[] ReadArguments(ParseResult result, List<Argument<string>> arguments)
    {
        var values = new List<string>(arguments.Count);
        foreach (var argument in arguments)
        {
            if (result.GetValue(argument) is { } value)
            {
                values.Add(value);
            }
        }

        return [.. values];
    }
}
