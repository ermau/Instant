//
// Display.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Instant
{
	public class SkippedIteration
	{
	}

	public static class Display
	{
		public static string Object (object value)
		{
			if (value == null)
				return "null";

			return Displayers.GetOrAdd (value.GetType(), CreateDisplay) (value);
		}

		private static readonly ConcurrentDictionary<Type, Func<object, string>> Displayers = new ConcurrentDictionary<Type, Func<object, string>>();

		private static readonly Regex DisplayExpressionsRegex = new Regex (@"\{(.+?)\}", RegexOptions.Compiled);
		private static Func<object, string> CreateDisplay (Type type)
		{
			DebuggerDisplayAttribute[] attrs = type.GetCustomAttributes (typeof (DebuggerDisplayAttribute), inherit: true).Cast<DebuggerDisplayAttribute>().ToArray();
			if (attrs.Length == 0)
				return GetStringForObject;

			DebuggerDisplayAttribute topAttr = attrs[0];

			MatchCollection matches = DisplayExpressionsRegex.Matches (topAttr.Value);
			if (matches.Count == 0)
				return GetStringForObject;

			List<Func<object, object>> getters = new List<Func<object, object>>();
			foreach (Match m in matches)
			{
				string expr = m.Value.Substring (1, m.Value.Length - 2).Replace ("(", String.Empty).Replace (")", String.Empty);

				MemberInfo[] members = type.GetMember (expr,
					MemberTypes.Field | MemberTypes.Method | MemberTypes.Property,
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				if (members.Length == 1)
				{
					MethodInfo method = null;

					PropertyInfo property = members [0] as PropertyInfo;
					if (property != null)
						method = property.GetGetMethod();

					if (method == null)
						method = members [0] as MethodInfo;

					if (method != null && method.ReturnType != typeof (void))
					{
						getters.Add (o => method.Invoke (o, null));
						continue;
					}

					FieldInfo field = members [0] as FieldInfo;
					if (field != null)
					{
						getters.Add (field.GetValue);
						continue;
					}
				}

				getters.Add (o => m.Value);
			}

			return o =>
			{
				StringBuilder builder = new StringBuilder (topAttr.Value);
				
				for (int i = 0; i < matches.Count; ++i)
					builder.Replace (matches [i].Value, GetStringForObject (getters [i] (o)));

				return builder.ToString();
			};
		}

		private static string GetStringForObject (object value)
		{
			if (value is SkippedIteration)
				return String.Empty;
			if (value == null)
				return "null";
			if (value is string)
				return "\"" + TrimTo (value, 100) + "\"";
			if (value is char)
				return "'" + TrimTo (value, 100) + "'";

			Type t = value.GetType();
			if (t.IsArray)
				return GetStringForArray (value);

			return TrimTo (value, 100);
		}

		private static string TrimTo (object value, int max)
		{
			string str = value.ToString();
			if (str.Length > max)
				str = str.Substring (0, max);

			return str;
		}

		private static string GetStringForArray (object array)
		{
			StringBuilder arrayBuilder = new StringBuilder ("| ");

			Array a = (Array) array;
			for (int i = 0; i < a.Length; ++i)
			{
				arrayBuilder.Append (GetStringForObject (a.GetValue (i)));
				arrayBuilder.Append (" | ");
			}

			return arrayBuilder.ToString();
		}
	}
}