using Microsoft.CodeAnalysis;
using System.Linq;

namespace ResponseTypeAttributeValidator
{
    internal static class Extensions
    {
        internal static bool TypeEquals(this INamedTypeSymbol attributeType, INamedTypeSymbol returnType)
        {
            if (ReferenceEquals(returnType, null)) return false;

            // If we are different variable types life is simple
            if ((attributeType.IsReferenceType && returnType.IsValueType) || (attributeType.IsValueType && returnType.IsReferenceType)) return false;

            // Same story if one type is generic and the other isn't. 
            if ((attributeType.IsGenericType && !returnType.IsGenericType) || (!attributeType.IsGenericType && returnType.IsGenericType)) return false;

            if (attributeType.IsGenericType)
            {
                // Check Liskov for the outer type, i.e. List<Car> and IEnumerable<Car>,ICollection<Car>,IList<Car>, etc... 
                bool isAssignableFrom = IsAssignableFrom(returnType, attributeType);
                if (!isAssignableFrom) return false;

                // Then recursively check the inner type arguments
                int count = attributeType.TypeArguments.Count();
                if (count != returnType.TypeArguments.Count()) return false;
                
                for(var i = 0; i < count; i++)
                {
                    // There will be some problem here if we have a nested array as type arguments
                    var one = attributeType.TypeArguments[i] as INamedTypeSymbol;
                    var two = returnType.TypeArguments[i] as INamedTypeSymbol;
                    if (!one.TypeEquals(two)) return false;
                }
                return true;
            }
            else
            {
                return IsAssignableFrom(returnType, attributeType);
            }

        }

        internal static bool TypeEquals(this INamedTypeSymbol attributeType, IArrayTypeSymbol returnType)
        {
            if (!returnType.OriginalDefinition.AllInterfaces.Any(i => i.OriginalDefinition.Equals(attributeType.ConstructedFrom))) return false;

            for(INamedTypeSymbol baseType = (INamedTypeSymbol)returnType.ElementType; baseType != null; baseType = baseType.BaseType?.OriginalDefinition)
            {
                if (baseType.Equals(attributeType.TypeArguments[0].OriginalDefinition)) return true;
            }
            return false;

        }

        private static bool IsAssignableFrom(INamedTypeSymbol type, INamedTypeSymbol parentType)
        {
            for (INamedTypeSymbol baseType = type.OriginalDefinition; baseType != null; baseType = baseType.BaseType?.OriginalDefinition)
            {
                if (baseType.Equals(parentType.OriginalDefinition)) return true;
            }

            if (type.OriginalDefinition.AllInterfaces.Any(i => i.OriginalDefinition.Equals(parentType.ConstructedFrom))) return true;

            return false;
        }
    }
}
