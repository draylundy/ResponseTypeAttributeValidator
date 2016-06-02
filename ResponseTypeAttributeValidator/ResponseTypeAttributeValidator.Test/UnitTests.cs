using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ResponseTypeAttributeValidator.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {

        [TestMethod]
        public void ShouldNotReportDiagnosticsWhenSameType()
        {
            var test = GetFile("Car", "Car");
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ShouldNotReportDiagnosticsWhenEmbeddedType()
        {
            var test = GetFile("CustomResponse<Car>", "CustomResponse<Car>");
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ShouldNotReportDiagnosticsWhenEmbeddedEnumberableType()
        {
            var test = GetFile("CustomResponse<IEnumerable<Car>>", "CustomResponse<List<Car>>");
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ShouldNotReportDiagnosticsWhenDerivedType()
        {
            var test = GetFile("Auto", "Car");
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void ShouldReportDiagnosticsWhenDifferentTypes()
        {
            var test = GetFile("Cat", "Car");

            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Cat' is not consistent with real return type 'TestNamespace.Car'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34),
                            new DiagnosticResultLocation("Test0.cs", 23, 31)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = GetFile("Car", "Car");

        }

        [TestMethod]
        public void ShouldReportDiagnosticsWhenNoReturnType()
        {
            var test = GetFile("Car", string.Empty);

            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Car' is not consistent with real return type 'No Return Type'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34),
                            new DiagnosticResultLocation("Test0.cs", 20, 28),
                            new DiagnosticResultLocation("Test0.cs", 23, 28)
                            
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = GetFile("Car", "Car");
        }

        [TestMethod]
        public void ShouldReportDiagnosticsWhenExpectingUnmatchedIEnumerableType()
        {
            var test = GetFile("IEnumerable<Car>", "Car");
            
            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'System.Collections.Generic.IEnumerable<TestNamespace.Car>' is not consistent with real return type 'TestNamespace.Car'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34),
                            new DiagnosticResultLocation("Test0.cs", 23, 31)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ShouldReportDiagnosticWhenReturningUnmatchedIEnumerableType()
        {

            var test = GetFile("Car", "List<Car>");
            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Car' is not consistent with real return type 'System.Collections.Generic.List<TestNamespace.Car>'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34),
                            new DiagnosticResultLocation("Test0.cs", 23, 31)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        [TestMethod]
        public void ShouldReportDiagnosticWhenParentType()
        {

            var test = GetFile("Car", "Auto");
            var expected = new DiagnosticResult
            {
                Id = "ResponseTypeAttributeValidator",
                Message = "Attribute return type 'TestNamespace.Car' is not consistent with real return type 'TestNamespace.Auto'.",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 15, 34),
                            new DiagnosticResultLocation("Test0.cs", 23, 31)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
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