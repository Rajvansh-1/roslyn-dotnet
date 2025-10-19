// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.SimplifyPropertyPattern;

/// <summary>
/// Analyzer for simplifying nested property patterns in C# 10.0+.
/// 
/// Converts code of the form:
/// <code>x is { a: { b: ... } }</code>
/// 
/// Into a simpler form:
/// <code>x is { a.b: ... }</code>
/// </summary>
/// <remarks>
/// Only works for projects targeting C# 10 or higher. Disabled otherwise.
/// Improves readability and reduces unnecessary nesting in property patterns.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class CSharpSimplifyPropertyPatternDiagnosticAnalyzer : AbstractBuiltInCodeStyleDiagnosticAnalyzer
{
    public CSharpSimplifyPropertyPatternDiagnosticAnalyzer()
        : base(
            IDEDiagnosticIds.SimplifyPropertyPatternDiagnosticId,
            EnforceOnBuildValues.SimplifyPropertyPattern,
            CSharpCodeStyleOptions.PreferExtendedPropertyPattern,
            new LocalizableResourceString(nameof(CSharpAnalyzersResources.Property_pattern_can_be_simplified), CSharpAnalyzersResources.ResourceManager, typeof(CSharpAnalyzersResources)),
            new LocalizableResourceString(nameof(CSharpAnalyzersResources.Simplify_property_pattern), CSharpAnalyzersResources.ResourceManager, typeof(CSharpAnalyzersResources)))
    {
    }

    public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
        => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

    protected override void InitializeWorker(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Dotted property patterns are only available in C# 10.0 and above.
            // Do not offer this refactoring for lower versions.
            if (compilationContext.Compilation.LanguageVersion() < LanguageVersion.CSharp10)
                return;

            compilationContext.RegisterSyntaxNodeAction(AnalyzeSubpattern, SyntaxKind.Subpattern);
        });
    }

    private void AnalyzeSubpattern(SyntaxNodeAnalysisContext syntaxContext)
    {
        // Bail immediately if the user has disabled this feature.
        var styleOption = syntaxContext.GetCSharpAnalyzerOptions().PreferExtendedPropertyPattern;
        if (!styleOption.Value || ShouldSkipAnalysis(syntaxContext, styleOption.Notification))
            return;

        var subpattern = (SubpatternSyntax)syntaxContext.Node;
        if (!SimplifyPropertyPatternHelpers.IsSimplifiable(subpattern, out _, out var expressionColon))
            return;

        // Report diagnostic at the location of the colon expression for better clarity.
        syntaxContext.ReportDiagnostic(DiagnosticHelper.Create(
            Descriptor,
            expressionColon.GetLocation(),
            styleOption.Notification,
            syntaxContext.Options,
            additionalLocations: new[] { subpattern.GetLocation() },
            properties: null));
    }
}
