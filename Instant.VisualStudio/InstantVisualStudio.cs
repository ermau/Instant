//
// InstantVisualStudio.cs
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Cadenza.Collections;
using EnvDTE;
using Instant.Operations;
using Instant.VisualStudio.ViewModels;
using Instant.VisualStudio.Views;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Task = System.Threading.Tasks.Task;

namespace Instant.VisualStudio
{
	public class InstantVisualStudio
	{
		public InstantVisualStudio (IWpfTextView view)
		{
			this.view = view;
			this.layer = view.GetAdornmentLayer("Instant.VisualStudio");

			//Listen to any event that changes the layout (text changes, scrolling, etc)
			this.view.LayoutChanged += OnLayoutChanged;

			this.dispatcher = Dispatcher.CurrentDispatcher;
		}

		private readonly IAdornmentLayer layer;
		private readonly IWpfTextView view;

		private CancellationTokenSource cancelSource = new CancellationTokenSource();
		private readonly Dispatcher dispatcher;

		private ExecutionContext context;

		private class ExecutionContext
		{
			public string MethodSignature;
			public ITrackingSpan Span;
			public ITextVersion Version;
			public string TestCode;
			public IDictionary<int, MethodCall> LastData;
			public IDictionary<int, ITextSnapshotLine> LineMap;
		}

		private readonly BidirectionalDictionary<ITrackingSpan, FrameworkElement> adorners = new BidirectionalDictionary<ITrackingSpan, FrameworkElement>();
		private readonly _DTE dte = (_DTE)Package.GetGlobalService (typeof (DTE));

		/// <summary>
		/// On layout change add the adornment to any reformatted lines
		/// </summary>
		private void OnLayoutChanged (object sender, TextViewLayoutChangedEventArgs e)
		{
			if (this.context != null)
			{
				Span currentSpan = this.context.Span.GetSpan (e.NewSnapshot);
				if (e.NewOrReformattedSpans.Any (s => currentSpan.Contains (s)))
				{
					if (this.context.Version != e.NewSnapshot.Version) // Text changed
					{
						this.context.LineMap = null;
						this.context.Version = e.NewSnapshot.Version;
						Execute (e.NewSnapshot, GetCancelSource().Token);
					}
					else
						AdornCode (e.NewSnapshot, GetCancelSource (current: true).Token);
				}
			}

			LayoutButtons (e.NewSnapshot, GetCancelSource (current: true).Token);
		}

		// We can likely set up a cache for all these, just need to ensure they're
		// cleared when the user changes them.
		private EnvDTE.Properties FontsAndColors
		{
			get { return this.dte.Properties["FontsAndColors", "TextEditor"]; }
		}

		private FontsAndColorsItems FontsAndColorsItems
		{
			get { return ((FontsAndColorsItems)FontsAndColors.Item ("FontsAndColorsItems").Object); }
		}

		private float FontSize
		{
			get { return (float)FontsAndColors.Item ("FontSize").Value; }
		}

		private Brush BorderBrush
		{
			get { return GetBrush (FontsAndColorsItems.Item ("Keyword").Foreground); }
		}

		private Brush Foreground
		{
			get { return GetBrush (FontsAndColorsItems.Item ("Plain Text").Foreground); }
		}

		private FontFamily FontFamily
		{
			get
			{
				string family = (string)FontsAndColors.Item("FontFamily").Value;
				return new FontFamily (family);
			}
		}

		private SolidColorBrush GetBrush (uint color)
		{
			int oleColor = Convert.ToInt32 (color);
			return new SolidColorBrush (Color.FromRgb (
					(byte)((oleColor) & 0xFF),
					(byte)((oleColor >> 8) & 0xFF),
					(byte)((oleColor >> 16) & 0xFF)));
		}

		private CancellationTokenSource GetCancelSource (bool current = false)
		{
			if (current)
			{
				var source = new CancellationTokenSource();
				var currentSource = Interlocked.CompareExchange (ref this.cancelSource, source, null);
				return currentSource ?? source;
			}

			var cancel = new CancellationTokenSource();
			CancellationTokenSource oldCancel = Interlocked.Exchange (ref this.cancelSource, cancel);
			if (oldCancel != null)
			{
				oldCancel.Cancel();
				oldCancel.Dispose();
			}

			return cancel;
		}

		private void LayoutButtons (ITextSnapshot newSnapshot, CancellationToken cancelToken)
		{
			Task.Factory.StartNew (s =>
			{
				var snapshot = (ITextSnapshot)s;

				string code = snapshot.GetText();

				CompilationUnitSyntax root = SyntaxTree.ParseText (code, cancellationToken: cancelToken).GetRoot (cancelToken);
				
				if (root.GetDiagnostics().Any (d => d.Info.Severity == DiagnosticSeverity.Error))
					return;

				foreach (var m in root.DescendantNodes (n => !(n is MethodDeclarationSyntax)).OfType<MethodDeclarationSyntax>())
				{
					if (cancelToken.IsCancellationRequested)
						return;

					Location location = m.GetLocation();

					int index = location.SourceSpan.Start;
					ITextSnapshotLine line = snapshot.GetLineFromPosition (index);
					
					// TODO: Fix this for multi-line method signatures
					string methodSignature = line.GetText();
					if (this.context != null && methodSignature != this.context.MethodSignature)
						continue;
					
					this.dispatcher.BeginInvoke ((Action)(() =>
					{
						if (cancelToken.IsCancellationRequested)
							return;

						Span methodSpan = Span.FromBounds (index, location.SourceSpan.End + 1);
						ITrackingSpan tracking;
						
						Button button = FindAdorner<Button> (methodSpan, this.view.TextSnapshot, out tracking);

						bool preexisting = false;
						if (tracking == null)
						{
							tracking = snapshot.CreateTrackingSpan (methodSpan, SpanTrackingMode.EdgeExclusive);
							button = new Button();
							button.FontSize = FontSize * 0.90;
							button.Cursor = Cursors.Arrow;
						}
						else
							preexisting = true;

						if (this.context == null || methodSignature != this.context.MethodSignature)
						{
							if (!preexisting)
								button.Click += OnClickInstant;

							button.Content = "Instant";
							button.Tag = new ExecutionContext
							{
								MethodSignature = methodSignature,
								Span = tracking
							};
						}
						else
						{
							if (!preexisting)
								button.Click += OnClickStopInstant;

							button.Content = "Stop Instant";
						}

						SnapshotSpan span = new SnapshotSpan (this.view.TextSnapshot, line.Start, line.Length);

						Geometry g = this.view.TextViewLines.GetMarkerGeometry (span, true, new Thickness());
						if (g != null)
						{
							Canvas.SetLeft (button, g.Bounds.Right + 10);
							Canvas.SetTop (button, g.Bounds.Top);
							button.MaxHeight = g.Bounds.Height;

							if (!preexisting)
							{
								this.adorners[tracking] = button;
								this.layer.AddAdornment (AdornmentPositioningBehavior.TextRelative, span, null, button, AdornerRemoved);
							}
						}
					}));
				}
			}, newSnapshot, cancelToken);
		}

		private void AdornerRemoved (object tag, UIElement element)
		{
			this.adorners.Inverse.Remove ((FrameworkElement)element);
		}

		private void OnClickStopInstant (object sender, RoutedEventArgs e)
		{
			this.context = null;
			this.layer.RemoveAllAdornments();
			LayoutButtons (this.view.TextSnapshot, GetCancelSource().Token);
		}

		private void OnClickInstant (object sender, RoutedEventArgs e)
		{
			var window = new TestCodeWindow();
			window.Owner = Application.Current.MainWindow;
			string testCode = window.ShowForTestCode();
			if (testCode == null)
				return;

			Button b = (Button)sender;
			this.context = (ExecutionContext)b.Tag;
			this.context.TestCode = testCode;

			this.layer.RemoveAllAdornments();

			var snapshot = this.view.TextSnapshot;

			var source = GetCancelSource();
			LayoutButtons (snapshot, source.Token);
			Execute (snapshot, source.Token);
		}

		private static readonly Regex IdRegex = new Regex (@"/\*_(\d+)_\*/", RegexOptions.Compiled);
		private void Execute (ITextSnapshot snapshot, CancellationToken cancelToken)
		{
			try
			{
				string code = this.context.Span.GetText (snapshot);

				Instantly.InstrumentAndEvaluate (code, this.context.TestCode, cancelToken).ContinueWith (t =>
				{
					if (t.IsCanceled || t.IsFaulted)
						return;

					var methods = t.Result ?? this.context.LastData;
					if (methods == null || methods.Count == 0)
						return;

					// BUG: These can arrive out of order
					this.dispatcher.BeginInvoke ((Action<ITextSnapshot,string,IDictionary<int,MethodCall>,CancellationToken>)
						((s,c,m,ct) =>
						{
							this.context.LastData = m;
							AdornCode (s, c, m, ct);
						}),
						snapshot, code, methods, cancelToken);
				});
			}
			catch (Exception) // We don't have a way to show errors right now
			{
			}
		}

		private void AdornCode (ITextSnapshot snapshot, CancellationToken token)
		{
			if (this.context == null || this.context.LastData == null)
				return;

			AdornCode (snapshot, this.context.Span.GetText (snapshot), this.context.LastData, token);
		}

		private void AdornCode (ITextSnapshot snapshot, string code, IDictionary<int, MethodCall> methods, CancellationToken cancelToken)
		{
			try
			{
				if (this.context.LineMap == null)
				{
					if ((this.context.LineMap = ConstructLineMap (snapshot, cancelToken, code)) == null)
						return;
				}

				// TODO: Threads
				MethodCall container = methods.Values.First();
				AdornOperationContainer (container, snapshot, this.context.LineMap, cancelToken);

				foreach (ViewCache viewCache in this.views.Values)
				{
					InstantView[] cleared = viewCache.ClearViews();
					for (int i = 0; i < cleared.Length; i++)
						this.layer.RemoveAdornment (cleared[i]);
				}
			}
			catch (OperationCanceledException)
			{
			}
		}

		private Dictionary<int, ITextSnapshotLine> ConstructLineMap (ITextSnapshot snapshot, CancellationToken cancelToken, string code)
		{
			SyntaxNode root = SyntaxTree.ParseText (code, cancellationToken: cancelToken).GetRoot (cancelToken);
			//root = new FixingRewriter().Visit (root);
			var ided = new IdentifyingVisitor().Visit (root);

			var lineMap = new Dictionary<int, ITextSnapshotLine>();

			string line;
			int ln = snapshot.GetLineNumberFromPosition (this.context.Span.GetStartPoint (this.view.TextSnapshot));
			StringReader reader = new StringReader (ided.ToString());
			while ((line = reader.ReadLine()) != null)
			{
				MatchCollection matches = IdRegex.Matches (line);
				foreach (Match match in matches)
				{
					cancelToken.ThrowIfCancellationRequested();

					int id;
					if (!Int32.TryParse (match.Groups[1].Value, out id))
						continue;

					ITextSnapshotLine lineSnapshot = snapshot.GetLineFromLineNumber (ln);
					lineMap[id] = lineSnapshot;
				}

				ln++;
			}

			if (lineMap.Count == 0)
				return null;

			return lineMap;
		}

		private readonly Dictionary<Type, ViewCache> views = new Dictionary<Type, ViewCache>();
		private void AdornOperationContainer (OperationContainer container, ITextSnapshot snapshot, IDictionary<int, ITextSnapshotLine> lineMap, CancellationToken cancelToken)
		{
			foreach (Operation operation in container.Operations)
			{
				ITextSnapshotLine line;
				if (!lineMap.TryGetValue (operation.Id, out line))
					continue;

				Geometry g = this.view.TextViewLines.GetMarkerGeometry (line.Extent);
				if (g == null)
					continue;

				Type opType = operation.GetType();

				OperationVisuals vs = Mapping[opType];

				ViewCache viewCache;
				if (!views.TryGetValue (opType, out viewCache))
					views[opType] = viewCache = new ViewCache (vs);

				InstantView adorner;
				bool preexisted = viewCache.TryGetView (out adorner);
				if (!preexisted)
				{
					adorner.FontSize = FontSize - 1;
					adorner.FontFamily = FontFamily;
					adorner.BorderBrush = BorderBrush;
					adorner.Foreground = Foreground;
				}

				adorner.Tag = operation.Id;

				OperationViewModel model = adorner.DataContext as OperationViewModel;
				if (model == null)
					adorner.DataContext = model = vs.CreateViewModel();

				model.Operation = operation;

				if (operation is Loop)
				{
					var loopModel = (LoopViewModel)model;

					LoopIteration[] iterations = loopModel.Iterations;
					if (!preexisted || loopModel.Iteration > iterations.Length - 1)
						loopModel.Iteration = iterations.Length;

					if (!preexisted)
					{
						loopModel.IterationChanged += (sender, args) =>
						{
							LoopIteration iteration = args.PreviousIteration;
							if (iteration != null)
							{
								HashSet<int> removes = new HashSet<int>();
								foreach (Operation op in iteration.Operations)
								{
									if (removes.Contains (op.Id))
										continue;

									ViewCache cache = this.views[op.GetType()];
									InstantView opAdorner = cache.GetView (op.Id);
									if (opAdorner != null)
										this.layer.RemoveAdornment (opAdorner);
								}
							}

							AdornCode (this.view.TextSnapshot, GetCancelSource (current: true).Token);
						};
					}

					if (iterations.Length > 0)
						AdornOperationContainer (iterations[loopModel.Iteration - 1], snapshot, lineMap, cancelToken);
				}

				Canvas.SetLeft (adorner, g.Bounds.Right + 10);
				Canvas.SetTop (adorner, g.Bounds.Top + 1);
				adorner.Height = g.Bounds.Height - 2;
				adorner.MaxHeight = g.Bounds.Height - 2;

				if (!preexisted)
					this.layer.AddAdornment (AdornmentPositioningBehavior.TextRelative, line.Extent, null, adorner, OperationAdornerRemoved);
			}
		}

		private void OperationAdornerRemoved (object tag, UIElement element)
		{
			InstantView adorner = (InstantView)element;
			OperationViewModel vm = (OperationViewModel)adorner.DataContext;

			ViewCache cache;
			if (!this.views.TryGetValue (vm.Operation.GetType(), out cache))
				return;

			cache.Remove (adorner);
		}

		private static readonly Dictionary<Type, OperationVisuals> Mapping = new Dictionary<Type, OperationVisuals>
		{
			{ typeof(StateChange), OperationVisuals.Create (() => new StateChangeView(), () => new StateChangeViewModel()) },
			{ typeof(ReturnValue), OperationVisuals.Create (() => new ReturnValueView(), () => new ReturnValueViewModel()) },
			{ typeof(Loop), OperationVisuals.Create (() => new LoopView(), () => new LoopViewModel()) }
		};

		private T FindAdorner<T> (Span span, ITextSnapshot snapshot, out ITrackingSpan tracking)
			where T : FrameworkElement
		{
			return (T)FindAdorner (typeof (T), span, snapshot, out tracking);
		}

		private FrameworkElement FindAdorner (Type viewType, Span span, ITextSnapshot snapshot, out ITrackingSpan tracking)
		{
			tracking = null;

			foreach (var kvp in this.adorners)
			{
				Span s = kvp.Key.GetSpan (snapshot);
				if (!s.Contains (span) || kvp.Value.GetType() != viewType)
					continue;

				tracking = kvp.Key;
				return kvp.Value;
			}

			return null;
		}
	}
}
