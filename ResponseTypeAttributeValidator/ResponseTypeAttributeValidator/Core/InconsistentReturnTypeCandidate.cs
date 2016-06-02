using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace ResponseTypeAttributeValidator
{
    internal class InconsistentReturnTypeCandidate
    {
        public ReturnStatementSyntax ReturnStatement { get; private set; }
        public ITypeSymbol ReturnType { get; private set; }
        public bool IsConsistent { get; private set; }

        public InconsistentReturnTypeCandidate(ReturnStatementSyntax node, ITypeSymbol namedType, bool consistent)
        {
            ReturnStatement = node;
            ReturnType = namedType;
            IsConsistent = consistent;
        }

        public bool HasReturnType
        {
            get
            {
                return !ReferenceEquals(ReturnType, null);
            }
        }

        public ArgumentSyntax ReturnArgument
        {
            get
            {
                return ReturnStatement.DescendantNodes().SingleOrDefault(n => n.Parent.IsKind(SyntaxKind.ArgumentList)) as ArgumentSyntax;
            }
        }

        public InvocationExpressionSyntax ReturnInvocation
        {
            get
            {
                return ReturnStatement.DescendantNodes().SingleOrDefault(n => n.IsKind(SyntaxKind.InvocationExpression)) as InvocationExpressionSyntax;
            }
        }
    }
}
