using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

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

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, TwoTypeMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

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
                var analyzer = new MismatchedReturnTypeAnalyzer(attribute);

                // Interested in the return statement to compare the actual return type with the attribute ResponseType
                startCodeBlockContext.RegisterSyntaxNodeAction(analyzer.AnalyzeSyntaxNode, SyntaxKind.ReturnStatement);

                // And here is where we'll set the Diagnostic if we find a mismatch
                startCodeBlockContext.RegisterCodeBlockEndAction(analyzer.RegisterDiagnostic);
            });
        }

        private class MismatchedReturnTypeCandidate
        {
            public ReturnStatementSyntax ReturnStatement { get; private set; }
            public ITypeSymbol ReturnType { get; private set; }
            public bool IsConsistent { get; private set; }

            public bool HasReturnType
            {
                get
                {
                    return !ReferenceEquals(ReturnType, null);
                }
            }

            public MismatchedReturnTypeCandidate(ReturnStatementSyntax node, ITypeSymbol namedType, bool consistent)
            {
                ReturnStatement = node;
                ReturnType = namedType;
                IsConsistent = consistent;
            }

        }

        private class MismatchedReturnTypeAnalyzer
        {
            #region Per-Codeblock Mutable State
            private readonly AttributeData _attribute;
            private readonly INamedTypeSymbol _attributeReturnType;
            private ITypeSymbol _returnType;
            private IList<MismatchedReturnTypeCandidate> _candidates;
            #endregion 

            private bool IsConsistent()
            {
                // If we have a ResponseType attribute and no response types returned
                if (_candidates.All(t => !t.HasReturnType)) return false;

                // If we have a ResponseType attribute and no responses with that type
                if (_candidates.Any(t => t.HasReturnType && !t.IsConsistent)) return false;

                return true;
            }

            public MismatchedReturnTypeAnalyzer(AttributeData attribute)
            {
                _candidates = new List<MismatchedReturnTypeCandidate>();
                _attribute = attribute;
                _attributeReturnType = (INamedTypeSymbol)attribute.ConstructorArguments.SingleOrDefault().Value;
            }

            internal void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext nodeContext)
            {
                var semmodel = nodeContext.SemanticModel;

                /*
                 * Get the real return type from the type arguments. This will be some method on the ApiController; e.g. Ok(new Car()),
                 * which actually creates a OkNegotiatedContentResult<Car>(new Car()), and it's the TypeArgument ('T') that
                 * we read to get the real return type. 
                 * 
                 * This is necessary as there are multiple methods with different sets of arguments, so we don't 
                 * want to be parsing method parameters to find the correct type. 
                 */
                var typeInfo = semmodel.GetTypeInfo(nodeContext.Node.ChildNodes().SingleOrDefault(), nodeContext.CancellationToken);
                _returnType = (typeInfo.Type as INamedTypeSymbol)?.TypeArguments.SingleOrDefault();

                /*
                 * If we've gotten this far but don't have a parametric return type we just have a non-generic return type; 
                 * i.e. return Ok() instead of Ok(new Car()). That's a problem since we have a ResponseType attribute. 
                 */
                  
                if (ReferenceEquals(_returnType, null))
                {
                    _candidates.Add(new MismatchedReturnTypeCandidate((ReturnStatementSyntax)nodeContext.Node, _returnType, true));
                    return;
                }

                /*
                 * This is a bit ugly but necessary as IArrayTypeSymbol doesn't have generic checks (why would it?), but we
                 * must account for people returning arrays since it does implement IEnumerable and this is frequently done. 
                 */

                bool consistent = true;
                if (_returnType.Kind.Equals(SymbolKind.ArrayType))
                {
                    consistent = _attributeReturnType.TypeEquals((IArrayTypeSymbol)_returnType);
                    
                }
                else if(_returnType.Kind.Equals(SymbolKind.NamedType))
                {
                    consistent = _attributeReturnType.TypeEquals((INamedTypeSymbol)_returnType);
                }

                _candidates.Add(new MismatchedReturnTypeCandidate((ReturnStatementSyntax)nodeContext.Node, _returnType, consistent));
            }

            internal void RegisterDiagnostic(CodeBlockAnalysisContext obj)
            {
                if (IsConsistent()) return;

                // TODO : Handle the diagnostics better now that we're watching all the return sites and not just the first one.

                var syntaxNode = _attribute.ApplicationSyntaxReference.GetSyntax();
                var typeOf = syntaxNode.DescendantNodes().SingleOrDefault(t => t.Kind().Equals(SyntaxKind.TypeOfExpression));
                var args = typeOf.ChildNodes().SingleOrDefault();
                var diagnostic = Diagnostic.Create(Rule, args.GetLocation(), _attributeReturnType?.ToDisplayString(), 
                    ReferenceEquals(_returnType, null) ? "No Return Type" : _returnType.ToDisplayString());

                obj.ReportDiagnostic(diagnostic);
            }

        }

    }
}
