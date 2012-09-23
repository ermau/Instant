using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Instant.VisualStudio
{
	/// <summary>
	/// Interaction logic for TestCodeWindow.xaml
	/// </summary>
	public partial class TestCodeWindow : Window
	{
		public TestCodeWindow()
		{
			InitializeComponent();
			code.Focus();
		}

		public string ShowForTestCode()
		{
			bool? accepted = ShowDialog();
			if (accepted == null || !accepted.Value)
				return null;

			return this.code.Text;
		}

		private void OnClickOk (object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void OnClickCancel (object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
