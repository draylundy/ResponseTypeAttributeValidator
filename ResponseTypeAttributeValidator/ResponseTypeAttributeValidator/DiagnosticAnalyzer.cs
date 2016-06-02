using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ResponseTypeAttributeValidator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ResponseTypeAttributeValidatorAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ResponseTypeAttributeValidator";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and TwoTypeMessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString TwoTypeMessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormatWithReturnType), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, TwoTypeMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartAction<SyntaxKind>(startCodeBlockContext =>
            {
                // Don't care about non-method code blocks
                if (startCodeBlockContext.OwningSymbol.Kind != SymbolKind.Method) return;

                // Or non-async methods, as they won't be wrapped in Task<TResult>
                // https://github.com/dotnet/roslyn/blob/master/src/Compilers/Core/Portable/Symbols/IMethodSymbol.cs
                var method = (IMethodSymbol)startCodeBlockContext.OwningSymbol;
                if (!method.IsAsync) return;

                // Or async methods that aren't WebApi; i.e. don't return Task<IHttpActionResult>
                // https://github.com/dotnet/roslyn/blob/master/src/Compilers/Core/Portable/Symbols/INamedTypeSymbol.cs
                var returnType = (INamedTypeSymbol)method.ReturnType;
                var isTaskIHttpActionResult = returnType.Name.Equals("Task") && returnType.IsGenericType && (returnType.TypeArguments.SingleOrDefault()?.Name?.Equals("IHttpActionResult")).GetValueOrDefault(false);
                if (!isTaskIHttpActionResult) return;

                // Or async Task<IHttpActionResult> methods where we don't see a ResponseType attribute
                // https://github.com/dotnet/roslyn/blob/master/src/Compilers/CSharp/Portable/Symbols/Attributes/AttributeData.cs
                var attribute = method.GetAttributes().SingleOrDefault(x => x.AttributeClass.Name.Equals("ResponseTypeAttribute"));
                if (ReferenceEquals(null, attribute)) return;

                // We now have a candidate so let's get the return type wrapped in IHttpActionResult<T>, if it's not there return
                // Note that TypedConstant is a struct, don't worry about null in this SingleOrDefault call. 
                var returnAttributeType = (INamedTypeSymbol)attribute.ConstructorArguments.SingleOrDefault().Value;
                if (ReferenceEquals(null, returnAttributeType)) return;

                // So now let's create our analyzer and register it for actions
                var analyzer = new InconsistentReturnTypeAnalyzer(attribute);

                // Interested in the return statement to compare the actual return type with the attribute ResponseType
                startCodeBlockContext.RegisterSyntaxNodeAction(analyzer.AnalyzeSyntaxNode, SyntaxKind.ReturnStatement);

                // And here is where we'll set the Diagnostic if we find a mismatch
                startCodeBlockContext.RegisterCodeBlockEndAction(analyzer.RegisterDiagnostic);
            });
        }
    }
}
