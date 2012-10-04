//
// Hook.cs
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
using System.Threading;
using Instant.Operations;

namespace Instant
{
	public static class Hook
	{
		public static Submission CreateSubmission (IInstrumentationSink instrumentationSink, CancellationToken cancelToken)
		{
			lock (SubmissionLock)
			{
				int id = Interlocked.Increment (ref currentSubmission);

				if (submission != null && submission.SubmissionId > id)
					throw new OperationCanceledException (cancelToken);

				return submission = new Submission (id, instrumentationSink);
			}
		}

		public static CancellationToken CancelToken;

		public static void BeginLoop (int submissionId, int id)
		{
			var sink = GetSink (submissionId);
			sink.BeginLoop (id);
		}

		public static void BeginInsideLoop (int submissionId, int id)
		{
			var sink = GetSink (submissionId);
			sink.BeginInsideLoop (id);

			if (CancelToken.IsCancellationRequested)
			{
				EndLoop (submissionId, id);
				throw new OperationCanceledException (CancelToken);
			}
		}

		public static void EndInsideLoop (int submissionId, int id)
		{
			var sink = GetSink (submissionId);
			sink.EndInsideLoop (id);
			
			if (CancelToken.IsCancellationRequested)
			{
				EndLoop (submissionId, id);
				throw new OperationCanceledException (CancelToken);
			}
		}

		public static void EndLoop (int submissionId, int id)
		{
			var sink = GetSink (submissionId);
			sink.EndLoop (id);
		}

		public static void LogReturn (int submissionId, int id)
		{
			var sink = GetSink (submissionId);
			sink.LogReturn (id);
		}

		public static T LogReturn<T> (int submissionId, int id, T value)
		{
			var sink = GetSink (submissionId);
			sink.LogReturn (id, value);

			return value;
		}

		public static T LogObject<T> (int submissionId, int id, string name, T value)
		{
			var sink = GetSink (submissionId);
			sink.LogVariableChange (id, name, value);

			return value;
		}
		
		public static T LogPostfix<T> (int submissionId, int id, T expression, string name, T newValue)
		{
			var sink = GetSink (submissionId);
			sink.LogVariableChange (id, name, newValue);

			return expression;
		}

		public static void LogEnterMethod (int submissionId, int id, string name, params StateChange[] arguments)
		{
			var sink = GetSink (submissionId);
			sink.LogEnterMethod (id, name, arguments);
		}

		private static readonly object SubmissionLock = new object();
		private static int currentSubmission;
		private static Submission submission;

		private static IInstrumentationSink GetSink (int submissionId)
		{
			Submission s = submission;
			if (s == null || s.SubmissionId != submissionId)
				throw new OperationCanceledException();

			return s.Sink;
		}
	}
}