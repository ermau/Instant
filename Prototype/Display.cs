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
using System.Text;

namespace LiveCSharp
{
	public class SkippedIteration
	{
	}

	public static class Display
	{
		public static string Object (object value)
		{
			return Displayers.GetOrAdd (value.GetType(), CreateDisplay) (value);
		}

		private static readonly ConcurrentDictionary<Type, Func<object, string>> Displayers = new ConcurrentDictionary<Type, Func<object, string>>();

		private static Func<object, string> CreateDisplay (Type type)
		{
			return GetStringForObject;
		}

		private static string GetStringForObject (object value)
		{
			if (value is SkippedIteration)
				return String.Empty;
			if (value == null)
				return "null";

			Type t = value.GetType();
			if (t.IsArray)
				return GetStringForArray (value);

			return value.ToString();
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