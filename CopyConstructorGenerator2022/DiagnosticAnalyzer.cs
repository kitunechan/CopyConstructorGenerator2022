using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CopyConstructorGenerator2022 {
	[DiagnosticAnalyzer( LanguageNames.CSharp )]
	public class CopyConstructorGenerator2022Analyzer : DiagnosticAnalyzer {
		public const string DiagnosticId = "CopyConstructorGenerator2022";

		internal const string Title = "CopyConstructorGenerator2022";
		internal const string MessageFormat = "コピーコンストラクタを作成します。";
		internal const string Description = "コピーコンストラクタを作成します。";
		private const string Category = "Constructor";

		internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor( DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Hidden, isEnabledByDefault: true, description: Description );


		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create( Rule ); } }

		public override void Initialize( AnalysisContext context ) {
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis( GeneratedCodeAnalysisFlags.None );

			context.RegisterSyntaxNodeAction( AnalyzeSymbol, SyntaxKind.ClassDeclaration );
		}

		private static void AnalyzeSymbol( SyntaxNodeAnalysisContext context ) {
			if( context.Node is ClassDeclarationSyntax classDeclaration ) {

				var diagnostic = Diagnostic.Create( Rule, classDeclaration.Identifier.GetLocation() );
				context.ReportDiagnostic( diagnostic );
			}
		}


	}
}
