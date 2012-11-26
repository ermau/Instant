//
// Extensions.cs
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
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.NRefactory.CSharp;
namespace Instant
{
	public static class Extensions
	{
		public static void InsertBefore (this BlockStatement self, AstNode nextSibling, Expression expression)
		{
			if (self == null)
				throw new ArgumentNullException ("self");
			if (nextSibling == null)
				throw new ArgumentNullException ("nextSibling");
			if (expression == null)
				throw new ArgumentNullException ("expression");

			self.InsertChildBefore (nextSibling, new ExpressionStatement (expression), BlockStatement.StatementRole);
		}

		public static Identifier FindIdentifier (this AstNode node)
		{
			var ident = node as IdentifierExpression;
			if (ident != null)
				return ident.IdentifierToken;

			var init = node as VariableInitializer;
			if (init != null)
				return init.NameToken;

			var assignment = node as AssignmentExpression;
			if (assignment != null)
				return FindIdentifier (assignment.Left);

			var unary = node as UnaryOperatorExpression;
			if (unary != null)
				return FindIdentifier (unary.Expression);

			return null;
		}

		public static IEnumerable<T> Concat<T> (this IEnumerable<T> self, T concated)
		{
			foreach (T element in self)
				yield return element;

			yield return concated;
		}

		public static IEnumerable<T> Prepend<T> (this IEnumerable<T> self, T prepended)
		{
			yield return prepended;

			foreach (T element in self)
				yield return element;
		}

		public static async Task<List<T>> ToListAsync<T> (this IEnumerable<Task<T>> self)
		{
			List<T> list = new List<T>();
			foreach (Task<T> task in self)
				list.Add (await task.ConfigureAwait (false));

			return list;
		}
	}
}