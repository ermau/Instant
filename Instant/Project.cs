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