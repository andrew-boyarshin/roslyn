﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.PreferFrameworkType
{
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic,
        Name = PredefinedCodeFixProviderNames.PreferFrameworkType), Shared]
    internal class PreferFrameworkTypeCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public PreferFrameworkTypeCodeFixProvider()
        {
        }

        public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            IDEDiagnosticIds.PreferBuiltInOrFrameworkTypeDiagnosticId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var diagnostic = context.Diagnostics[0];
            if (diagnostic.Properties.ContainsKey(PreferFrameworkTypeConstants.PreferFrameworkType))
            {
                context.RegisterCodeFix(new PreferFrameworkTypeCodeAction(GetDocumentUpdater(context)), context.Diagnostics);
            }

            return Task.CompletedTask;
        }

        protected override async Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CodeActionOptionsProvider fallbackOptions, CancellationToken cancellationToken)
        {
            var generator = document.GetLanguageService<SyntaxGenerator>();
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in diagnostics)
            {
                var node = diagnostic.Location.FindNode(
                    findInsideTrivia: true, getInnermostNodeForTie: true, cancellationToken);

                if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol is ITypeSymbol typeSymbol)
                {
                    var replacementNode = generator.TypeExpression(typeSymbol).WithTriviaFrom(node);
                    editor.ReplaceNode(node, replacementNode);
                }
            }
        }

        protected override bool IncludeDiagnosticDuringFixAll(Diagnostic diagnostic)
            => diagnostic.Properties.ContainsKey(PreferFrameworkTypeConstants.PreferFrameworkType);

        private class PreferFrameworkTypeCodeAction : CodeAction.DocumentChangeAction
        {
            public PreferFrameworkTypeCodeAction(
                Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(FeaturesResources.Use_framework_type, createChangedDocument, FeaturesResources.Use_framework_type)
            {
            }
        }
    }
}
