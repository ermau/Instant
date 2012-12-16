//
// MethodDeclarationExtensions.cs
//
// Copyright 2012 Eric Maupin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Text;
using ICSharpCode.NRefactory.CSharp;

namespace Instant
{
	public static class MethodDeclarationExtensions
	{
		public static string GetExampleInvocation (this MethodDeclaration method)
		{
			if (method == null)
				throw new ArgumentNullException ("method");

			StringBuilder builder = new StringBuilder();

			AstNode entity = method;
			NamespaceDeclaration ns = null;
			TypeDeclaration type = null;

			while (ns == null)
			{
				if (entity.Parent == null)
					break;

				entity = entity.Parent;

				if (type == null)
					type = entity as TypeDeclaration;

				ns = entity as NamespaceDeclaration;
			}

			if (type == null)
				return null;

			if (!method.Modifiers.HasFlag (Modifiers.Static))
			{
				builder.Append ("var obj = new ");
				
				if (ns != null)
				{
					builder.Append (ns.FullName);
					builder.Append (".");
				}

				builder.Append (type.Name);
				builder.AppendLine (" ();");

				builder.Append ("obj.");
			}
			else
			{
				if (ns != null)
				{
					builder.Append (ns.FullName);
					builder.Append (".");
				}

				builder.Append (type.Name);
				builder.Append (".");
				
			}

			builder.Append (method.Name);
			builder.Append (" (");

			BuildParameters (method.Parameters, builder);

			builder.Append (");");

			return builder.ToString();
		}

		private static void BuildParameters (IEnumerable<ParameterDeclaration> parameters, StringBuilder builder)
		{
			bool first = true;
			foreach (ParameterDeclaration parameterDeclaration in parameters)
			{
				if (!first)
					builder.Append (", ");
				else
					first = false;

				PrimitiveType primitive = parameterDeclaration.Type as PrimitiveType;
				if (primitive != null)
					builder.Append (GetTestValueForPrimitive (primitive));
				else
				{
					builder.Append ("default(");
					builder.Append (parameterDeclaration.Type.ToString());
					builder.Append (")");
				}
			}
		}

		private static string GetTestValueForPrimitive (PrimitiveType type)
		{
			switch (type.Keyword)
			{
				case "char":
					return "'a'";
				
				case "uint":
				case "int":
				case "ushort":
				case "short":
				case "byte":
				case "sbyte":
				case "ulong":
				case "long":
					return "1";

				case "decimal":
					return "1.1m";
				case "float":
					return "1.1f";
				case "double":
					return "1.1";

				case "string":
					return "\"test\"";

				default:
					throw new ArgumentException();
			}
		}
	}
}
