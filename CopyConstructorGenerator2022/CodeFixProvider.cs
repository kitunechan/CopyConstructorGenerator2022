﻿using Microsoft.CodeAnalysis.Formatting;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;


namespace CopyConstructorGenerator2022 {
	[ExportCodeFixProvider( LanguageNames.CSharp, Name = nameof( CopyConstructorGeneratorAnalyzerCodeFixProvider ) ), Shared]
	public class CopyConstructorGeneratorAnalyzerCodeFixProvider : CodeFixProvider {


		public sealed override ImmutableArray<string> FixableDiagnosticIds {
			get { return ImmutableArray.Create( CopyConstructorGenerator2022Analyzer.DiagnosticId ); }
		}

		public sealed override FixAllProvider GetFixAllProvider() {
			return WellKnownFixAllProviders.BatchFixer;
		}

		readonly string[] _Modifiers = new[] { "const", "static" };

		public sealed override async Task RegisterCodeFixesAsync( CodeFixContext context ) {

			var model = await context.Document.GetSemanticModelAsync( context.CancellationToken );

			var root = await context.Document.GetSyntaxRootAsync( context.CancellationToken ).ConfigureAwait( false ) as CompilationUnitSyntax;

			var diagnostic = context.Diagnostics.First();
			var diagnosticSpan = diagnostic.Location.SourceSpan;
			var classDeclaration = root.FindToken( diagnosticSpan.Start ).Parent.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

			var className = classDeclaration.Identifier.Text;


			if( classDeclaration.Members.Any() ) {
				// コード編集を登録します。
				context.RegisterCodeFix(
					CodeAction.Create( "コピーコンストラクタの作成",
						token => {
							var values = classDeclaration.Members
											.Where( x => {
												switch( x ) {
													case PropertyDeclarationSyntax prop: {
														return prop.AccessorList?.Accessors.Any( z => z.IsKind( SyntaxKind.GetAccessorDeclaration ) ) == true;
													}
													case FieldDeclarationSyntax field: {
														return !field.Modifiers.Any( z => _Modifiers.Contains( z.Text ) );
													}
													default: {
														return false;
													}
												}
											} )
											.Select( x => {
												if( x is PropertyDeclarationSyntax prop ) {
													var name = prop.Identifier.Text;

													return CreateCopyValue( model, prop.Type, name );
												} else if( x is FieldDeclarationSyntax field ) {
													var f = field.Declaration;
													var name = f.Variables.First().Identifier.Text;

													return CreateCopyValue( model, f.Type, name );
												}

												throw new Exception();
											} );

							var newRegionConst = CreateCopyConstructor( className, values );

							var newClassDeclaration = classDeclaration
									.ReplaceNode( r => r.Members.First(), f => f.WithLeadingTrivia( f.GetLeadingTrivia().AddRange( Enumerable.Range( 0, 2 ).Select( x => SyntaxFactory.ElasticCarriageReturnLineFeed ) ) ) )
									.InsertNodesBefore( r => r.Members.First(), newRegionConst )
									.ReplaceNode( r => r.Members.First(), r => r.WithAdditionalAnnotations( Formatter.Annotation ) );

							var newRoot = root.ReplaceNode( classDeclaration, newClassDeclaration );

							var newDocument = context.Document.WithSyntaxRoot( newRoot );

							return Task.FromResult( newDocument );
						} ),
					diagnostic );

				context.RegisterCodeFix(
					CodeAction.Create( "コピーコンストラクタの作成 (プロパティのみ)",
						token => {
							var values = classDeclaration.Members.OfType<PropertyDeclarationSyntax>()
											.Where( x => x.AccessorList?.Accessors.Any( z => z.IsKind( SyntaxKind.GetAccessorDeclaration ) ) == true )
											.Select( x => {
												var name = x.Identifier.Text;
												return CreateCopyValue( model, x.Type, name );
											} );

							var newRegionConst = CreateCopyConstructor( className, values );

							var newClassDeclaration = classDeclaration
									.ReplaceNode( r => r.Members.First(), f => f.WithLeadingTrivia( f.GetLeadingTrivia().AddRange( Enumerable.Range( 0, 2 ).Select( x => SyntaxFactory.ElasticCarriageReturnLineFeed ) ) ) )
									.InsertNodesBefore( r => r.Members.First(), newRegionConst )
									.ReplaceNode( r => r.Members.First(), r => r.WithAdditionalAnnotations( Formatter.Annotation ) );

							var newRoot = root.ReplaceNode( classDeclaration, newClassDeclaration );

							var newDocument = context.Document.WithSyntaxRoot( newRoot );

							return Task.FromResult( newDocument );
						} ),
					diagnostic );
			}
		}

		string CreateCopyValue( SemanticModel model, TypeSyntax type, string valueName ) {
			return $"this.{valueName} = {GetDeepInstance( model, type, $"value.{valueName}" )};";
		}

		readonly string[] genericArgs = new string[] { "x", "z", "k" };

		string GetDeepInstance( SemanticModel model, TypeSyntax type, string value, int count = 0 ) {
			if( type is GenericNameSyntax generic ) {

				// List と　Dictionary
				switch( generic.Identifier.Text ) {
					case "List": {
						var arg = generic.TypeArgumentList.Arguments.First();
						if( arg is PredefinedTypeSyntax ) {
							return $"{value}.ToList()";
						} else {
							var T = ( count < genericArgs.Length ) ? genericArgs[count] : "x" + count;

							return $"{value}.Select({T}=> {GetDeepInstance(model, arg, $"{T}", count + 1 )} ).ToList()";
						}
					}

					case "Dictionary": {
						if( generic.TypeArgumentList.Arguments.Any( x => !( x is PredefinedTypeSyntax ) ) ) {
							var T = ( count < genericArgs.Length ) ? genericArgs[count] : "x" + count;

							var keyType = generic.TypeArgumentList.Arguments[0];
							var valueType = generic.TypeArgumentList.Arguments[1];

							var k = ( count == 0 ) ? "k" : "k" + count;
							var v = ( count == 0 ) ? "v" : "v" + count;

							return $"{value}.ToDictionary( {k}=> {GetDeepInstance(model, keyType, $"{k}.Key", count + 1 )}, {v}=> {GetDeepInstance(model, valueType, $"{v}.Value", count + 1 )} )";
						} else {
							return $"{value}.ToDictionary( k=>k.Key, v=>v.Value )";
						}
					}

					default:
						break;
				}

				// Generic型の何か
				return $"new {type.ToString()}({value}. )";
			}

			// struct or class
			
			switch( type ) {
				case IdentifierNameSyntax t: {
					var symbol = model.GetSymbolInfo(type).Symbol as ITypeSymbol;
					switch( symbol.TypeKind ) {
						case TypeKind.Enum:
						case TypeKind.Struct:
							return value;
					}

					break;
				}
				case PredefinedTypeSyntax _:
				case NullableTypeSyntax _:
					return value;
			}

			return $"new {type.ToString()}({value})";
		}

		/// <summary>

		/// </summary>
		IEnumerable<MemberDeclarationSyntax> CreateCopyConstructor( string className, IEnumerable<string> values ) {
			var regionSource =
				"/// <summary>\r\n" +
				"/// コピーコンストラクタ\r\n" +
				"/// </summary>\r\n" +
				$"public { className } ( {className} value )" + "{" + $@"
					{ string.Join( "\r\n", values.ToArray() )}
				" + "}";
			return CSharpSyntaxTree.ParseText( regionSource ).GetRoot()
									.ChildNodes()
									.OfType<MemberDeclarationSyntax>();
		}

	}
}