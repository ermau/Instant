//
// IProject.cs
//
// Copyright 2012 Eric Maupin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cadenza;

namespace Instant
{
	public interface IProject
	{
		/// <summary>
		/// Gets the combined defined constants.
		/// </summary>
		string DefinedConstants { get; }

		/// <summary>
		/// Gets whether /unsafe is enabled.
		/// </summary>
		bool AllowUnsafe { get; }

		/// <summary>
		/// Gets whether /optimize is enabled.
		/// </summary>
		bool Optimize { get; }

		IEnumerable<string> References { get; }
		IEnumerable<Either<FileInfo,string>> Sources { get; }
	}

	public static class ProjectExtensions
	{
		public static string GetCompilerOptions (this IProject self)
		{
			if (self == null)
				throw new ArgumentNullException ("self");

			StringBuilder builder = new StringBuilder();
			if (self.AllowUnsafe)
				builder.Append (" /unsafe");

			if (self.Optimize)
				builder.Append (" /optimize");

			if (!String.IsNullOrWhiteSpace (self.DefinedConstants))
			{
				builder.Append (" /define:");
				builder.Append (self.DefinedConstants);
			}

			builder.Append (" /debug");

			return builder.ToString();
		}
	}
}