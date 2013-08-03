using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Instant
{
	static partial class InstantCommands
	{
		static InstantCommands()
		{
			NavigateToFrame = new DelegatedCommand<StackFrame> (NavigateToStackFrame, sf => sf != null);
		}

		private static void NavigateToStackFrame(StackFrame stackFrame)
		{
			VsShellUtilities.OpenDocument (ServiceProvider.GlobalProvider, stackFrame.File);
			_DTE dte = (_DTE)Package.GetGlobalService (typeof (DTE));
			dte.ActiveDocument.Selection.GotoLine (stackFrame.Line);
		}
	}
}