using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using Instant.ViewModels;

namespace Instant.VisualStudio
{
	/// <summary>
	/// Interaction logic for ExceptionView.xaml
	/// </summary>
	public partial class ExceptionView : UserControl
	{
		public ExceptionView()
		{
			InitializeComponent();
		}

		private void OnFrameSelected (object sender, MouseButtonEventArgs e)
		{
			object item = this.frames.SelectedItem;
			if (item == null)
				return;

			StackFrame frame = (StackFrame)item;

			InstantCommands.NavigateToFrame.Execute (frame);
		}
	}
}
