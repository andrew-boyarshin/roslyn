﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeCleanup;
using Roslyn.Utilities;

#if !CODE_STYLE
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;
#endif

namespace Microsoft.CodeAnalysis.Formatting
{
    internal interface ISyntaxFormatting
    {
        SyntaxFormattingOptions DefaultOptions { get; }
        SyntaxFormattingOptions GetFormattingOptions(AnalyzerConfigOptions options, SyntaxFormattingOptions? fallbackOptions);

        ImmutableArray<AbstractFormattingRule> GetDefaultFormattingRules();
        IFormattingResult GetFormattingResult(SyntaxNode node, IEnumerable<TextSpan>? spans, SyntaxFormattingOptions options, IEnumerable<AbstractFormattingRule>? rules, CancellationToken cancellationToken);
    }

    [DataContract]
    internal sealed record class LineFormattingOptions(
        [property: DataMember(Order = 0)] bool UseTabs = false,
        [property: DataMember(Order = 1)] int TabSize = 4,
        [property: DataMember(Order = 2)] int IndentationSize = 4,
        string? NewLine = null)
    {
        [property: DataMember(Order = 3)]
        public string NewLine { get; init; } = NewLine ?? Environment.NewLine;

        public static readonly LineFormattingOptions Default = new();

        public static LineFormattingOptions Create(AnalyzerConfigOptions options, LineFormattingOptions? fallbackOptions)
        {
            fallbackOptions ??= Default;

            return new(
                UseTabs: options.GetEditorConfigOption(FormattingOptions2.UseTabs, fallbackOptions.UseTabs),
                TabSize: options.GetEditorConfigOption(FormattingOptions2.TabSize, fallbackOptions.TabSize),
                IndentationSize: options.GetEditorConfigOption(FormattingOptions2.IndentationSize, fallbackOptions.IndentationSize),
                NewLine: options.GetEditorConfigOption(FormattingOptions2.NewLine, fallbackOptions.NewLine));
        }

#if !CODE_STYLE
        public static async Task<LineFormattingOptions> FromDocumentAsync(Document document, LineFormattingOptions? fallbackOptions, CancellationToken cancellationToken)
        {
            var documentOptions = await document.GetAnalyzerConfigOptionsAsync(cancellationToken).ConfigureAwait(false);
            return Create(documentOptions, fallbackOptions);
        }
#endif
    }

    internal abstract class SyntaxFormattingOptions
    {
        [DataMember(Order = 0)]
        public readonly LineFormattingOptions LineFormatting;

        [DataMember(Order = 1)]
        public readonly bool SeparateImportDirectiveGroups;

        protected const int BaseMemberCount = 2;

        protected SyntaxFormattingOptions(
            LineFormattingOptions? lineFormatting,
            bool separateImportDirectiveGroups)
        {
            LineFormatting = lineFormatting ?? LineFormattingOptions.Default;
            SeparateImportDirectiveGroups = separateImportDirectiveGroups;
        }

        public abstract SyntaxFormattingOptions With(LineFormattingOptions lineFormatting);

        public bool UseTabs => LineFormatting.UseTabs;
        public int TabSize => LineFormatting.TabSize;
        public int IndentationSize => LineFormatting.IndentationSize;
        public string NewLine => LineFormatting.NewLine;

#if !CODE_STYLE
        public static SyntaxFormattingOptions GetDefault(HostLanguageServices languageServices)
            => languageServices.GetRequiredService<ISyntaxFormattingService>().DefaultOptions;

        public static ValueTask<SyntaxFormattingOptions> GetDefaultAsync(HostLanguageServices languageServices, CancellationToken _)
            => ValueTaskFactory.FromResult(GetDefault(languageServices));

        public static SyntaxFormattingOptionsProvider CreateProvider(CodeActionOptionsProvider options)
            => new((languageServices, _) => ValueTaskFactory.FromResult(options(languageServices).CleanupOptions?.FormattingOptions ?? GetDefault(languageServices)));

        public static SyntaxFormattingOptionsProvider CreateProvider(CodeCleanupOptionsProvider options)
            => new(async (languageServices, cancellationToken) => (await options(languageServices, cancellationToken).ConfigureAwait(false)).FormattingOptions);

        public static SyntaxFormattingOptions Create(OptionSet options, HostWorkspaceServices services, SyntaxFormattingOptions? fallbackOptions, string language)
        {
            var formattingService = services.GetRequiredLanguageService<ISyntaxFormattingService>(language);
            var configOptions = options.AsAnalyzerConfigOptions(services.GetRequiredService<IOptionService>(), language);
            return formattingService.GetFormattingOptions(configOptions, fallbackOptions);
        }
#endif
    }

#if !CODE_STYLE
    internal delegate ValueTask<SyntaxFormattingOptions> SyntaxFormattingOptionsProvider(HostLanguageServices languageServices, CancellationToken cancellationToken);

    internal static class SyntaxFormattingOptionsProviders
    {
        public static async ValueTask<SyntaxFormattingOptions> GetSyntaxFormattingOptionsAsync(this Document document, SyntaxFormattingOptions? fallbackOptions, CancellationToken cancellationToken)
        {
            var documentOptions = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
            return SyntaxFormattingOptions.Create(documentOptions, document.Project.Solution.Workspace.Services, fallbackOptions, document.Project.Language);
        }

        public static async ValueTask<SyntaxFormattingOptions> GetSyntaxFormattingOptionsAsync(this Document document, SyntaxFormattingOptionsProvider fallbackOptionsProvider, CancellationToken cancellationToken)
            => await GetSyntaxFormattingOptionsAsync(document, await fallbackOptionsProvider(document.Project.LanguageServices, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

        public static ValueTask<SyntaxFormattingOptions> GetSyntaxFormattingOptionsAsync(this Document document, CodeActionOptionsProvider fallbackOptionsProvider, CancellationToken cancellationToken)
            => GetSyntaxFormattingOptionsAsync(document, fallbackOptionsProvider(document.Project.LanguageServices).CleanupOptions?.FormattingOptions, cancellationToken);

        public static async ValueTask<SyntaxFormattingOptions> GetSyntaxFormattingOptionsAsync(this Document document, CodeCleanupOptionsProvider fallbackOptionsProvider, CancellationToken cancellationToken)
            => await GetSyntaxFormattingOptionsAsync(document, (await fallbackOptionsProvider(document.Project.LanguageServices, cancellationToken).ConfigureAwait(false)).FormattingOptions, cancellationToken).ConfigureAwait(false);
    }
#endif
}
