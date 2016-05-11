﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using VB = SonarLint.Rules.VisualBasic;
using CS = SonarLint.Rules.CSharp;
using CACS = Microsoft.CodeAnalysis.CSharp;
using SonarLint.Helpers;
using System.Threading.Tasks;

namespace SonarLint.UnitTest.Helpers
{
    [TestClass]
    public class DiagnosticAnalyzerContextHelperTest
    {
        private static void VerifyEmpty(string name, string content, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument(name, content);

                var compilation = document.Project.GetCompilationAsync().Result;

                var diagnostics = Verifier.GetDiagnostics(compilation, diagnosticAnalyzer);

                diagnostics.Should().HaveCount(0);
            }
        }

        private static async Task<bool> IsGeneratedAsync(string content, GeneratedCodeRecognizer generatedCodeRecognizer)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = workspace.CurrentSolution.AddProject("foo", "foo.dll", LanguageNames.CSharp)
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                    .AddMetadataReference(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location))
                    .AddDocument("Foo.cs", content);

                var compilation = document.Project.GetCompilationAsync().Result;
                var tree = await document.GetSyntaxTreeAsync();

                return tree.IsGenerated(generatedCodeRecognizer, compilation);
            }
        }

        [TestMethod]
        public void No_Issue_On_Generated_File_With_Generated_Name()
        {
            const string sourceCs =
@"namespace Generated
{
    class MyClass
    {
        void M()
        {
            ;;;;
        }
    }
}";
            VerifyEmpty("test.g.cs", sourceCs, new CS.EmptyStatement());

            const string sourceVb =
@"Module Module1
    Sub Main()
        Dim foo() As String ' Noncompliant
    End Sub
End Module";
            VerifyEmpty("test.g.vb", sourceVb, new VB.ArrayDesignatorOnVariable());
        }

        [TestMethod]
        public void No_Issue_On_Generated_File_With_AutoGeneratedComment()
        {
            var sourceCs =
@"// ------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.3053
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
// ------------------------------------------------------------------------------
namespace Generated
{
    class MyClass
    {
        void M()
        {
            ;;;;
        }
    }
}";
            VerifyEmpty("test.cs", sourceCs, new CS.EmptyStatement());

            sourceCs =
@"// <autogenerated />
namespace Generated
{
    class MyClass
    {
        void M()
        {
            ;;;;
        }
    }
}";
            VerifyEmpty("test.cs", sourceCs, new CS.EmptyStatement());


            const string sourceVb =
@"'------------------------------------------------------------------------------
' <auto-generated>
'     This code was generated by a tool.
'     Runtime Version:2.0.50727.4927
'
'     Changes to this file may cause incorrect behavior and will be lost if
'     the code is regenerated.
' </auto-generated>
'------------------------------------------------------------------------------
Module Module1
    Sub Main()
        Dim foo() As String ' Noncompliant
    End Sub
End Module";
            VerifyEmpty("test.vb", sourceVb, new VB.ArrayDesignatorOnVariable());
        }

        [TestMethod]
        public void No_Issue_On_Generated_File_With_ExcludedAttribute()
        {
            const string sourceCs =
@"namespace Generated
{
    class MyClass
    {
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        void M()
        {
            ;;;;
        }
    }
}";
            VerifyEmpty("test.cs", sourceCs, new CS.EmptyStatement());

            const string sourceVb =
@"Module Module1
    <System.Diagnostics.DebuggerNonUserCodeAttribute()>
    Sub Main()
        Dim foo() As String ' Noncompliant
    End Sub
End Module";
            VerifyEmpty("test.vb", sourceVb, new VB.ArrayDesignatorOnVariable());
        }

        [TestMethod]
        public void No_Issue_On_Partially_Generated_Legacy_WinForms_File()
        {
            const string format =
@"namespace PartiallyGenerated
{
    class MyClass
    {
        // For the time being, the whole file is considered as generated
        void HandWrittenEventHandler()
        {
            ;;;;
        }

#region {0}
        void GeneratedStuff()
        {
            ;;;;
        }
#endregion
    }
}";
            VerifyEmpty("test.cs", format.Replace("{0}", "Windows Form Designer generated code"), new CS.EmptyStatement());
            VerifyEmpty("test.cs", format.Replace("{0}", "Windows Form Designer GeNeRaTeD code"), new CS.EmptyStatement());
        }

        [TestMethod]
        public async Task IsGenerated_On_GeneratedTree()
        {
            const string source =
@"namespace Generated
{
    class MyClass
    {
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        void M()
        {
            ;;;;
        }
    }
}";

            Assert.IsTrue(await IsGeneratedAsync(source, SonarLint.Helpers.CSharp.GeneratedCodeRecognizer.Instance));
        }

        [TestMethod]
        public async Task IsGenerated_On_NonGeneratedTree()
        {
            const string source =
@"namespace NonGenerated
{
    class MyClass
    {
    }
}";

            Assert.IsFalse(await IsGeneratedAsync(source, SonarLint.Helpers.CSharp.GeneratedCodeRecognizer.Instance));
        }
    }
}
