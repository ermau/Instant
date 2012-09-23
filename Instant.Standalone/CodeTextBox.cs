//
// CodeTextBox.cs
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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Instant.Operations;
using Instant.Standalone.Adorners;
using Roslyn.Compilers.CSharp;

namespace Instant.Standalone
{
	public class CodeTextBox
		: TextBox
	{
		public CodeTextBox()
		{
			AcceptsReturn = true;
			AcceptsTab = true;
			VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

			AddHandler (ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)ScrollChangedEventHandler);
			DependencyPropertyDescriptor dpd = DependencyPropertyDescriptor.FromProperty (FontSizeProperty, typeof (CodeTextBox));
			dpd.AddValueChanged (this, (o, e) => Update());
		}

		public static readonly DependencyProperty MethodCallProperty =
			DependencyProperty.Register ("MethodCall", typeof (MethodCall), typeof (CodeTextBox), new PropertyMetadata (default(MethodCall), MethodCallChanged));

		public MethodCall MethodCall
		{
			get { return (MethodCall)GetValue (MethodCallProperty); }
			set { SetValue (MethodCallProperty, value); }
		}

		public static readonly DependencyProperty IdCodeProperty =
			DependencyProperty.Register ("IdCode", typeof (string), typeof (CodeTextBox), new PropertyMetadata (default(string)));

		public string IdCode
		{
			get { return (string)GetValue (IdCodeProperty); }
			set { SetValue (IdCodeProperty, value); }
		}

		private void ScrollChangedEventHandler (object sender, ScrollChangedEventArgs e)
		{
			Update();
		}

		protected override void OnTextChanged (TextChangedEventArgs e)
		{
			base.OnTextChanged (e);

			Update();
		}

		private bool updating;
		private void Update()
		{
			if (this.updating || this.MethodCall == null)
				return;

			this.updating = true;

			SyntaxNode root = Syntax.ParseCompilationUnit (Text);
			root = new FixingRewriter().Visit (root);
			var ided = new IdentifyingVisitor().Visit (root);
			this.IdCode = ided.ToString();

			Dictionary<int, int> lineMap = new Dictionary<int, int>();

			string line;
			int ln = 0;
			StringReader reader = new StringReader (ided.ToString());					
			while ((line = reader.ReadLine()) != null)
			{
				MatchCollection matches = IdRegex.Matches (line);
				foreach (Match match in matches)
				{
					int id;
					if (!Int32.TryParse (match.Groups [1].Value, out id))
						continue;

					lineMap [id] = ln;
				}

				ln++;
			}

			var layer = AdornerLayer.GetAdornerLayer (this);
			foreach (Adorner adorner in this.adorners.Values)
				layer.Remove (adorner);

			this.adorners.Clear();

			if (lineMap.Count > 0)
				AdornOperationContainer (this.MethodCall, lineMap, layer);

			this.updating = false;
		}

		private static readonly Regex IdRegex = new Regex (@"/\*_(\d+)_\*/", RegexOptions.Compiled);
		private void OnMethodCallChanged()
		{
			Update();
		}

		private readonly Dictionary<int, int> currentIterations = new Dictionary<int, int>();

		private void AdornOperationContainer (OperationContainer container, IDictionary<int, int> lineMap, AdornerLayer layer)
		{
			foreach (Operation operation in container.Operations)
			{
				int ln;
				if (!lineMap.TryGetValue (operation.Id, out ln))
					continue;
				//int ln = lineMap [operation.Id];

				int ch = GetCharacterIndexFromLineIndex (ln);
				int len = GetLineLength (ln);
				var endRect = GetRectFromCharacterIndex (ch + len - 1, true);

				Adorner adorner = null;

				if (operation is Loop)
				{
					var loop = (Loop)operation;

					int iteration;
					if (!this.currentIterations.TryGetValue (loop.Id, out iteration))
						iteration = 0;

					var loopadorner = new LoopAdorner (this, loop);
					if (iteration >= loopadorner.Iterations.Length)
						this.currentIterations[loop.Id] = iteration = 0;

					loopadorner.CurrentIteration = iteration;
					loopadorner.IterationChanged += (s, e) =>
					{
						this.currentIterations[loop.Id] = loopadorner.CurrentIteration;
						OnMethodCallChanged();
					};

					adorner = loopadorner;

					AdornOperationContainer (loop.Operations.OfType<LoopIteration>().ElementAt (iteration), lineMap, layer);
				}
				else if (operation is StateChange)
				{
					adorner = new StateChangeAdorner (this, (StateChange)operation);
				}
				else if (operation is ReturnValue)
				{
					adorner = new ReturnAdorner (this, (ReturnValue)operation);
				}

				if (adorner != null)
				{
					double delta = endRect.Height - (endRect.Height * 0.8);

					adorner.Margin = new Thickness (endRect.X + 2, endRect.Y + (delta / 2), 0, 0);
					adorner.MaxHeight = endRect.Height;

					this.adorners.Add (operation.Id, adorner);
					layer.Add (adorner);
				}
			}
		}

		private readonly Dictionary<int, Adorner> adorners = new Dictionary<int, Adorner>();

		private static void MethodCallChanged (DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
		{
			((CodeTextBox)dependencyObject).OnMethodCallChanged();
		}
	}
}
