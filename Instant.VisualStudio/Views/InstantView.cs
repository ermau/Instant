using System.Windows;
using System.Windows.Controls;

namespace Instant.VisualStudio.Views
{
	public class InstantView
		: Border
	{
		public static readonly DependencyProperty FontSizeProperty =
			DependencyProperty.Register ("FontSize", typeof (double), typeof (InstantView), new PropertyMetadata (12d));

		public double FontSize
		{
			get { return (double)GetValue (FontSizeProperty); }
			set { SetValue (FontSizeProperty, value); }
		}
	}
}