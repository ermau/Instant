using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Instant
{
	public class StackFrame
	{
		public StackFrame (string method, string file, int line)
		{
			if (file == null)
				throw new ArgumentNullException ("file");
			if (line <= 0)
				throw new ArgumentOutOfRangeException ("line");

			Method = method;
			File = file;
			Line = line;
		}

		public string Method
		{
			get;
			private set;
		}

		public string File
		{
			get;
			private set;
		}

		public int Line
		{
			get;
			private set;
		}

		private static readonly Regex FrameRegex = new Regex (
			@"\s*at\s(?<method>.+?)\sin\s(?<file>.+?):line\s(?<line>\d+)", RegexOptions.Compiled);

		public static bool TryParse (string line, out StackFrame frame)
		{
			frame = null;

			Match match = FrameRegex.Match (line);
			if (!match.Success)
				return false;

			string lineNumber = match.Groups["line"].Value;
			int ln;
			if (!Int32.TryParse (lineNumber, out ln))
				return false;

			string file = match.Groups["file"].Value;
			string method = match.Groups["method"].Value;

			frame = new StackFrame (method, file, ln);
			return true;
		}
	}
}
