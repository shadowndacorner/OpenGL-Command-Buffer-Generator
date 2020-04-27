using System;
using System.Text;
using System.Collections.Generic;

namespace GLThreadGen
{
    public class FunctionArgument
	{
		public FunctionArgument() { }
		public FunctionArgument(string argString)
		{
			for(int i = argString.Length - 1; i >= 0; --i)
			{
				if (!LineParser.IsValidIdentifierCharacter(argString[i]))
				{
					Type = argString.Substring(0, i + 1).Trim();
					Name = argString.Substring(i + 1).Trim();
					return;
				}
			}
		}

        public bool IsEnumType;
		public string Type;
		public string Name;
	}

	public class FunctionDeclaration
	{
		public string TypedefName;
		public bool IsReturnEnum;
		public string ReturnType;
		public List<FunctionArgument> Arguments = new List<FunctionArgument>();

		public override string ToString()
		{
			var build = new StringBuilder(ReturnType);
			build.Append(" ");
			build.Append(TypedefName);
			build.Append(" (");
			for (int i = 0; i < Arguments.Count; ++i)
			{
				var arg = Arguments[i];
				build.Append('(');
				build.Append(arg.Type);
				build.Append(')');
				build.Append(' ');
				build.Append('[');
				build.Append(arg.Name);
				build.Append(']');
				if (i < Arguments.Count - 1)
				{
					build.Append(", ");
				}
			}
			build.Append(")");
			return build.ToString();
		}
	}

	public class FunctionEntry
	{
		public FunctionDeclaration Type;
		public bool ReturnsHandle
		{
			get
			{
				return Type.ReturnType.EndsWith("Handle");
			}
		}

		public bool Returns
		{
			get
			{
				return Type.ReturnType != "void";
			}
		}

		public bool ShouldReturnAsFinalArgPointer
		{
			get
			{
				return Returns && !ReturnsHandle;
			}
		}
		
		private string _name;
		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
				NoGLName = value.Substring("gl".Length);
			}
		}

		public string Access { get; set; } = "public";
		
		public string NoGLName { get; private set; }

		public override string ToString()
		{
			var build = new StringBuilder(Type.ReturnType);
			build.Append(" ");
			build.Append(Name);
			build.Append(" (");
			for (int i = 0; i < Type.Arguments.Count; ++i)
			{
				var arg = Type.Arguments[i];
				build.Append(arg.Type);
				build.Append(' ');
				build.Append(arg.Name);
				if (i < Type.Arguments.Count - 1)
				{
					build.Append(", ");
				}
			}
			build.Append(")");
			return build.ToString();
		}
	}

    public class GLEnum
    {
        public string Name { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }
    
    public class GLDataRegistry
    {
		public HashSet<string> Defines = new HashSet<string>();
        public Dictionary<string, FunctionDeclaration> FunctionTypes = new Dictionary<string, FunctionDeclaration>();
		public Dictionary<string, FunctionEntry> Functions = new Dictionary<string, FunctionEntry>();
        public Dictionary<string, GLEnum> EnumTypes = new Dictionary<string, GLEnum>();
    }
}