//
// InstantView.cs
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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

		public static readonly DependencyProperty FontFamilyProperty =
			DependencyProperty.Register ("FontFamily", typeof (FontFamily), typeof (InstantView), new PropertyMetadata (default(FontFamily)));

		public FontFamily FontFamily
		{
			get { return (FontFamily)GetValue (FontFamilyProperty); }
			set { SetValue (FontFamilyProperty, value); }
		}

		public static readonly DependencyProperty ForegroundProperty =
			DependencyProperty.Register ("Foreground", typeof (Brush), typeof (InstantView), new PropertyMetadata (default(Brush)));

		public Brush Foreground
		{
			get { return (Brush)GetValue (ForegroundProperty); }
			set { SetValue (ForegroundProperty, value); }
		}
	}
}