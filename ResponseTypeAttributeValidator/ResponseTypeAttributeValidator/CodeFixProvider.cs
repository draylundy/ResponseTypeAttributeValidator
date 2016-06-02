using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CodeActions;
using System.Linq;
using System;

namespace ResponseTypeAttributeValidator
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ResponseTypeAttributeValidatorCodeFixProvider)), Shared]
    public class ResponseTypeAttributeValidatorCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Synchronize Attribute";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ResponseTypeAttributeValidatorAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration in the attribute identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeOfExpressionSyntax>().First();
            var declar = root.FindToken(diagnosticSpan.Start).Parent.Ancestors().OfType<MethodDeclarationSyntax>().First();
            var retState = declar.DescendantNodes().OfType<ReturnStatementSyntax>().First();

            var retTypeInfo = semanticModel.GetTypeInfo(retState.Expression);
            var retType = (retTypeInfo.Type as INamedTypeSymbol)?.TypeArguments.SingleOrDefault();

            if (ReferenceEquals(retType, null))
            {
                context.RegisterCodeFix(CodeAction.Create(
                    @"Remove ResponseType attribute."+Environment.NewLine+"WARNING: This action might change your public API.",
                    c => RemoveAttributeAsync(context.Document, declaration, c),
                    Title),
                    diagnostic);
            }
            else
            {
                context.RegisterCodeFix(
                CodeAction.Create(
                    $"Replace attribute value with real return type {retType.Name}." + Environment.NewLine + "WARNING: This action might change your public API.",
                    c => SynchronizeAttributeAsync(context.Document, declaration, (INamedTypeSymbol)retType, c),
                    Title),
                diagnostic);
            }    
        }

        private async Task<Document> RemoveAttributeAsync(Document document, TypeOfExpressionSyntax declaration, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            return document;
        }

        //private async Task<Solution> MakeUppercaseAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        private async Task<Solution> SynchronizeAttributeAsync(Document document, TypeOfExpressionSyntax typeDecl, INamedTypeSymbol realType, CancellationToken cancellationToken)
        {
            /*
            // Compute new uppercase name.
            var identifierToken = typeDecl;
            var newName = identifierToken.ToString();

            // Get the symbol representing the attribute type symbol
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.Options;
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            return newSolution;
            */
            return document.Project.Solution;
        }
    }
}