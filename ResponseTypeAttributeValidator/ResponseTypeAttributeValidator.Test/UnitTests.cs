using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using ResponseTypeAttributeValidator;

namespace ResponseTypeAttributeValidator.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {

        //No diagnostics expected to show up
        [TestMethod]
        public void ShouldNotThrowWhenSameType()
        {
            var test = GetFile("Car", "Car");

            VerifyCSharpDiagnostic(test);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public void ShouldNotThrowWhenEmbeddedType()
        {
            var test = GetFile("CustomResponse<Car>", "CustomResponse<Car>");

            VerifyCSharpDiagnostic(test);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public void ShouldNotThrowWhenEmbeddedEnumberableType()
        {
            var test = GetFile("CustomResponse<IEnumerable<Car>>", "CustomResponse<List<Car>>");
            VerifyCSharpDiagnostic(test);
        }

        //No diagnostics expected to show up
        [TestMethod]
        public void ShouldNotThrowWhenDerivedType()
        {
            var test = GetFile("Auto", "Car");

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ShouldThrowWhenDifferentTypes()
        {
            var test = GetFile("Cat", "Car");

            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Cat' is not consistent with real return type 'TestNamespace.Car'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = GetFile("Car", "Car");

            // No fix to verify yet
            VerifyCSharpFix(test, fixtest);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ShouldThrowWhenNoReturnType()
        {
            var test = GetFile("Car", string.Empty);

            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Car' is not consistent with real return type 'No Return Type'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = GetFile("Car", "Car");

            // No fix to verify yet
            VerifyCSharpFix(test, fixtest);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ShouldThrowWhenExpectingUnmatchedIEnumerableType()
        {
            var test = GetFile("IEnumerable<Car>", "Car");
            
            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'System.Collections.Generic.IEnumerable<TestNamespace.Car>' is not consistent with real return type 'TestNamespace.Car'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            // No fix to verify yet
            //VerifyCSharpFix(test, fixtest);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ShouldThrowWhenReturningUnmatchedIEnumerableType()
        {

            var test = GetFile("Car", "List<Car>");
            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Car' is not consistent with real return type 'System.Collections.Generic.List<TestNamespace.Car>'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            // No fix to verify yet
            //VerifyCSharpFix(test, fixtest);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void ShouldThrowWhenParentType()
        {

            var test = GetFile("Car", "Auto");
            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Car' is not consistent with real return type 'TestNamespace.Auto'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            // No fix to verify yet
            //VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new ResponseTypeAttributeValidatorCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ResponseTypeAttributeValidatorAnalyzer();
        }

        private string GetFile(string expectedType, string returnType)
        {
            var constructor = returnType.Equals(string.Empty) ? string.Empty : $@"new {returnType}()";
            return $@"
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using System.Web.Http.Description;

namespace TestNamespace
{{
    public class AccountController : ApiController
        {{
            [ResponseType(typeof({expectedType}))]
            public async Task<IHttpActionResult> GetCar()
            {{
                int flag = 0;
                if(flag==1){{
                    return BadRequest();
                }}
                else if(flag==2){{
                    return Ok({constructor});
                }}
            }}
        }}
    public class CustomResponse<T> {{}}
    public class Auto {{}}
    public class Car : Auto {{}}
    public class Cat{{}}
}}";
        }
    }
}