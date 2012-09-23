using System.Collections.Generic;
using System.Diagnostics;

namespace LiveCSharp
{
	public abstract class Operation
	{
		protected Operation (int id)
		{
			Id = id;
		}

		public int Id
		{
			get;
			private set;
		}
	}

	public abstract class OperationContainer
		: Operation
	{
		protected OperationContainer (int id)
			: base (id)
		{
		}

		public ICollection<Operation> Operations
		{
			get { return this.operations; }
		}

		private readonly List<Operation> operations = new List<Operation>();
	}

	public class MethodCall
		: OperationContainer
	{
		public MethodCall (int id, string name, IEnumerable<StateChange> arguments)
			: base (id)
		{
			MethodName = name;
			Arguments = arguments;
		}

		public string MethodName
		{
			get;
			private set;
		}

		public IEnumerable<StateChange> Arguments
		{
			get;
			private set;
		}
	}

	public class Loop
		: OperationContainer
	{
		public Loop (int id)
			: base (id)
		{
		}
	}

	public class LoopIteration
		: OperationContainer
	{
		public LoopIteration (int id)
			: base (id)
		{
		}
	}

	[DebuggerDisplay ("[{Id}] {Variable} = {Value}")]
	public class StateChange
		: Operation
	{
		public StateChange (int id, string variable, object value)
			: base (id)
		{
			Variable = variable;
			Value = Display.Object (value);
		}

		public StateChange (int id, string variable, string value)
			: base (id)
		{
			Variable = variable;
			Value = value;
		}

		public string Variable
		{
			get;
			private set;
		}

		public string Value
		{
			get;
			private set;
		}
	}

	[DebuggerDisplay ("return {Value}")]
	public class ReturnValue
		: Operation
	{
		public ReturnValue (int id, string value)
			: base (id)
		{
			Value = value;
		}

		public string Value
		{
			get;
			private set;
		}
	}
}
