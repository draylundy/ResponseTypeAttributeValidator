using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace ResponseTypeAttributeValidator
{
    internal class InconsistentReturnTypeAnalyzer
    {
        #region Per-Codeblock Mutable State
        private readonly AttributeData _attribute;
        private readonly INamedTypeSymbol _attributeReturnType;
        private ITypeSymbol _returnType;
        private IList<InconsistentReturnTypeCandidate> _candidates;
        #endregion

        public InconsistentReturnTypeAnalyzer(AttributeData attribute)
        {
            _candidates = new List<InconsistentReturnTypeCandidate>();
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
                _candidates.Add(new InconsistentReturnTypeCandidate((ReturnStatementSyntax)nodeContext.Node, _returnType, true));
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
            else if (_returnType.Kind.Equals(SymbolKind.NamedType))
            {
                consistent = _attributeReturnType.TypeEquals((INamedTypeSymbol)_returnType);
            }

            _candidates.Add(new InconsistentReturnTypeCandidate((ReturnStatementSyntax)nodeContext.Node, _returnType, consistent));
        }

        internal void RegisterDiagnostic(CodeBlockAnalysisContext obj)
        {
            var semModel = obj.SemanticModel;

            var diagnosticLocations = new List<Location>();
            // If we have a ResponseType attribute and no response types returned
            if (_candidates.All(t => !t.HasReturnType))
            {
                foreach (var candidate in _candidates)
                {
                    diagnosticLocations.Add(candidate.ReturnInvocation.GetLocation());
                }
            }
            // Or if we have a Responsetype attribute and we have an inconsistent response type
            else if (_candidates.Any(t => t.HasReturnType && !t.IsConsistent))
            {
                foreach (var candidate in _candidates.Where(t => t.HasReturnType && !t.IsConsistent))
                {
                    diagnosticLocations.Add(candidate.ReturnArgument.GetLocation());
                }
            }
            else
            {
                return;
            }

            // Now, assuming we handled one of the first two cases, report the diagnostic on the tribute
            var attributeSyntaxNode = _attribute.ApplicationSyntaxReference.GetSyntax();
            var typeOf = attributeSyntaxNode.DescendantNodes().SingleOrDefault(t => t.Kind().Equals(SyntaxKind.TypeOfExpression));
            var args = typeOf.ChildNodes().SingleOrDefault();

            // If we can't find a type in the typeof something is probably wrong, but we get out of the way. 
            if (ReferenceEquals(null, args)) return;

            var attributeDiagnostic = Diagnostic.Create(ResponseTypeAttributeValidatorAnalyzer.Rule, args.GetLocation(), diagnosticLocations, _attributeReturnType?.ToDisplayString(),
                ReferenceEquals(_returnType, null) ? "No Return Type" : _returnType.ToDisplayString());

            obj.ReportDiagnostic(attributeDiagnostic);
        }

    }
}
