//
// Project.cs
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

using System.Collections.Generic;
using System.IO;
using Cadenza;

namespace Instant
{
	public class Project
		: IProject
	{
		public string ConditionalCompilationSymbols
		{
			get;
			set;
		}

		public ICollection<string> References
		{
			get { return this.references; }
		}

		public ICollection<Either<FileInfo, string>> Sources
		{
			get { return this.sources; }
		}

		private readonly List<Either<FileInfo, string>> sources = new List<Either<FileInfo, string>>();
		private readonly List<string> references = new List<string>();

		IEnumerable<string> IProject.References
		{
			get { return References; }
		}

		IEnumerable<Either<FileInfo, string>> IProject.Sources
		{
			get { return Sources; }
		}
	}
}