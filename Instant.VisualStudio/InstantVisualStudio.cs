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
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Cadenza.Collections;
using EnvDTE;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;

namespace Instant.VisualStudio
{
	public class InstantVisualStudio
		: IDisposable
	{
		public InstantVisualStudio (IWpfTextView view)
		{
			this.view = view;
			this.layer = view.GetAdornmentLayer("Instant.VisualStudio");

			//Listen to any event that changes the layout (text changes, scrolling, etc)
			this.view.LayoutChanged += OnLayoutChanged;

			this.dispatcher = Dispatcher.CurrentDispatcher;

			this.evaluator.EvaluationCompleted += OnEvaluationCompleted;
			this.evaluator.Start();

			this.dte.Events.BuildEvents.OnBuildProjConfigDone += OnBuildProjeConfigDone;
			this.dte.Events.BuildEvents.OnBuildDone += OnBuildDone;
			this.dte.Events.BuildEvents.OnBuildBegin += OnBuildBegin;

			// HACK: Are we sure that it'll be the active document when created?
			this.document = this.dte.ActiveDocument;
		}

		public void Dispose()
		{
			CancellationTokenSource source = this.cancelSource;
			if (source != null)
				source.Dispose();

			this.evaluator.Dispose();
		}

		private Document document;

		private static int submissionId;
		private readonly Evaluator evaluator = new Evaluator();

		private readonly IAdornmentLayer layer;
		private readonly IWpfTextView view;

		private CancellationTokenSource cancelSource = new CancellationTokenSource();
		private readonly Dispatcher dispatcher;

		private ExecutionContext context;

		private class ExecutionContext
		{
			public string ExampleCode;
			public string MethodSignature;
			public ITrackingSpan Span;
			public ITextVersion Version;
			public string TestCode;
			public IDictionary<int, MethodCall> LastData;
			public IDictionary<int, ITextSnapshotLine> LineMap;
		}

		private readonly BidirectionalDictionary<ITrackingSpan, FrameworkElement> adorners = new BidirectionalDictionary<ITrackingSpan, FrameworkElement>();
		private readonly _DTE dte = (_DTE)Package.GetGlobalService (typeof (DTE));
		
		private bool buildSuccess;
		private void OnBuildBegin (vsBuildScope scope, vsBuildAction action)
		{
			this.buildSuccess = true;
		}

		private void OnBuildDone (vsBuildScope scope, vsBuildAction action)
		{
			if (this.context == null || !this.buildSuccess)
				return;

			var cancel = GetCancelSource();
			Execute (this.view.TextSnapshot, cancel.Token);
		}

		private void OnBuildProjeConfigDone (string project, string projectConfig, string platform, string solutionConfig, bool success)
		{
			if (this.buildSuccess)
				this.buildSuccess = success;
		}

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

		/// <summary>
		/// Gets a <see cref="CancellationTokenSource"/> and optionally cancels an old one.
		/// </summary>
		/// <param name="current">Whether or not to use the current source, if available.</param>
		private CancellationTokenSource GetCancelSource (bool current = false)
		{
			var source = new CancellationTokenSource();
			if (current)
			{
				var currentSource = Interlocked.CompareExchange (ref this.cancelSource, source, null);
				return currentSource ?? source;
			}

			CancellationTokenSource oldCancel = Interlocked.Exchange (ref this.cancelSource, source);
			if (oldCancel != null)
			{
				oldCancel.Cancel();
				oldCancel.Dispose();
			}

			return source;
		}

		private void LayoutButtons (ITextSnapshot newSnapshot, CancellationToken cancelToken)
		{
			Task.Factory.StartNew (s =>
			{
				var snapshot = (ITextSnapshot)s;

				string code = snapshot.GetText();

				SyntaxTree tree = SyntaxTree.Parse (code, cancellationToken: cancelToken);
				if (tree.Errors.Any (e => e.ErrorType == ErrorType.Error))
					return;

				foreach (var m in tree.Descendants.OfType<MethodDeclaration>())
				{
					if (cancelToken.IsCancellationRequested)
						return;

					ITextSnapshotLine startLine = snapshot.GetLineFromLineNumber (m.StartLocation.Line - 1);
					ITextSnapshotLine endLine = snapshot.GetLineFromLineNumber (m.EndLocation.Line - 1);
					
					// TODO: Fix this for multi-line method signatures
					string methodSignature = startLine.GetText();
					if (this.context != null && methodSignature != this.context.MethodSignature)
						continue;

					string exampleCode = m.GetExampleInvocation();

					this.dispatcher.BeginInvoke ((Action)(() =>
					{
						if (cancelToken.IsCancellationRequested)
							return;

						Span methodSpan = Span.FromBounds (startLine.Start.Position, endLine.End.Position);
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
								ExampleCode = exampleCode,
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

						SnapshotSpan span = new SnapshotSpan (this.view.TextSnapshot, startLine.Start, startLine.Length);

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
			Button b = (Button)sender;
			ExecutionContext bContext = (ExecutionContext)b.Tag;

			var window = new TestCodeWindow();
			window.Owner = Application.Current.MainWindow;
			string testCode = window.ShowForTestCode (bContext.ExampleCode);
			if (testCode == null)
				return;

			this.context = bContext;
			this.context.TestCode = testCode;

			this.layer.RemoveAllAdornments();

			var snapshot = this.view.TextSnapshot;

			var source = GetCancelSource();
			LayoutButtons (snapshot, source.Token);
			Execute (snapshot, source.Token);
		}

		private void OnEvaluationCompleted (object sender, EvaluationCompletedEventArgs e)
		{
			var sink = (MemoryInstrumentationSink)e.Submission.Sink;

			var methods = sink.GetRootCalls() ?? this.context.LastData;
			if (methods == null || methods.Count == 0)
				return;

			System.Tuple<ITextSnapshot, string> adornContext = (System.Tuple<ITextSnapshot,string>)e.Submission.Tag;

			this.dispatcher.BeginInvoke ((Action<ITextSnapshot,string,IDictionary<int,MethodCall>>)
				((s,c,m) =>
				{
					this.context.LastData = m;
					AdornCode (s, c, m);
				}),
				adornContext.Item1, adornContext.Item2, methods);
		}

		private async Task Execute (ITextSnapshot snapshot, CancellationToken cancelToken)
		{
			int id = Interlocked.Increment (ref submissionId);

			string original = snapshot.GetText();
			string code = await Instantly.Instrument (original, id);

			if (cancelToken.IsCancellationRequested || code == null)
				return;

			IProject project = this.dte.GetProject (this.document, code);

			Submission submission = null;
			var sink = new MemoryInstrumentationSink (() => submission.IsCanceled);
			submission = new Submission (id, project, sink, this.context.TestCode);
			submission.Tag = new System.Tuple<ITextSnapshot, string> (snapshot, original);

			this.evaluator.PushSubmission (submission);
		}

		private void AdornCode (ITextSnapshot snapshot, CancellationToken token = default(CancellationToken))
		{
			if (this.context == null || this.context.LastData == null)
				return;

			AdornCode (snapshot, snapshot.GetText(), this.context.LastData, token);
		}

		private void AdornCode (ITextSnapshot snapshot, string code, IDictionary<int, MethodCall> methods, CancellationToken cancelToken = default(CancellationToken))
		{
			try
			{
				if (this.context.LineMap == null)
				{
					if ((this.context.LineMap = ConstructLineMap (this.view.TextSnapshot, cancelToken, code)) == null)
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
			var tree = SyntaxTree.Parse (code, cancellationToken: cancelToken);
			var identifier = new IdentifyingVisitor();
			tree.AcceptVisitor (identifier);

			var lineMap = identifier.LineMap.ToDictionary (
				kvp => kvp.Key,
				kvp => snapshot.GetLineFromLineNumber (kvp.Value - 1) // VS lines are 0 based
				);

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

				if (this.view.TextViewLines.FormattedSpan.Snapshot.Version.VersionNumber > line.Extent.Snapshot.Version.VersionNumber)
					continue;

				Geometry g = this.view.TextViewLines.GetMarkerGeometry (line.Extent);
				if (g == null)
					continue;

				Type opType = operation.GetType();

				OperationVisuals vs;
				if (!Mapping.TryGetValue (opType, out vs))
					continue;

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

							ITextSnapshot s = this.view.TextSnapshot;
							var map = this.context.LineMap ?? ConstructLineMap (this.view.TextSnapshot, GetCancelSource (current: true).Token, this.context.Span.GetText(s));

							AdornOperationContainer (args.NewIteration, s, map, GetCancelSource (current: true).Token);
							//AdornCode (this.view.TextSnapshot, GetCancelSource (current: true).Token);
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
				if (!s.OverlapsWith (span) || kvp.Value.GetType() != viewType)
					continue;

				tracking = kvp.Key;
				return kvp.Value;
			}

			return null;
		}
	}
}
