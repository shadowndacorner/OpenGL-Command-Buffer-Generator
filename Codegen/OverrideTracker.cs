using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen
{
    public class CodegenOverrideTracker
    {
        public CodegenOverrideTracker(GLADHeaderParser parser)
        {
            Parser = parser;
        }

        public GLADHeaderParser Parser { get; private set; }

        public class OverrideMethods
        {
            public Action<FunctionEntry> ModifyFunctionEntry;
            public Func<CodegenContext, Func<Task>, FunctionEntry, Task> ModifyWriteFunction;
            public Func<CodegenContext, Func<Task>, FunctionEntry, Task> ModifyReadFunction;
        }

        public void Initialize()
        {
            foreach(var type in GetType().Assembly.ExportedTypes)
            {
                if (type != typeof(Overrides.GLOverride) && typeof(Overrides.GLOverride).IsAssignableFrom(type))
                {
                    var obj = (Overrides.GLOverride)Activator.CreateInstance(type);
                    obj.InitializeOverrides(this);
                }
            }
        }

        private Dictionary<string, List<OverrideMethods>> Overrides { get; set; } = new Dictionary<string, List<OverrideMethods>>();

        private List<OverrideMethods> GetOrCreateOverrideList(string func)
        {
            List<OverrideMethods> res;
            if (!Overrides.TryGetValue(func, out res))
            {
                res = new List<OverrideMethods>();
                Overrides.Add(func, res);
                return res;
            }
            return res;
        }

        public void RegisterOverride(string functionName, Action<FunctionEntry> overrideEntry = null, Func<CodegenContext, Func<Task>, FunctionEntry, Task> modifyReadFunc = null, Func<CodegenContext, Func<Task>, FunctionEntry, Task> modifyWriteFunc = null)
        {
            var list = GetOrCreateOverrideList(functionName);
            list.Add(new OverrideMethods { ModifyFunctionEntry = overrideEntry, ModifyWriteFunction = modifyWriteFunc, ModifyReadFunction = modifyReadFunc });
        }

        public delegate Task WriteOverrideDelegate(CodegenContext context, FunctionEntry function, FunctionArgument arg);
        public delegate Task ReadOverrideDelegate(CodegenContext context, FunctionEntry function, FunctionArgument arg);

        private Dictionary<string, ReadOverrideDelegate> ArgReadOverrides { get; set; } = new Dictionary<string, ReadOverrideDelegate>();
        public void RegisterArgumentTypeReadOverride(string type, ReadOverrideDelegate cb)
        {
            ArgReadOverrides[type] = cb;
        }

        public ReadOverrideDelegate GetArgumentTypeReadOverride(string type)
        {
            ReadOverrideDelegate res;
            ArgReadOverrides.TryGetValue(type, out res);
            return res;
        }

        private Dictionary<string, WriteOverrideDelegate> ArgWriteOverrides { get; set; } = new Dictionary<string, WriteOverrideDelegate>();
        public void RegisterArgumentTypeWriteOverride(string type, WriteOverrideDelegate cb)
        {
            ArgWriteOverrides[type] = cb;
        }

        public WriteOverrideDelegate GetArgumentTypeWriteOverride(string type)
        {
            WriteOverrideDelegate res;
            ArgWriteOverrides.TryGetValue(type, out res);
            return res;
        }

        public List<OverrideMethods> GetOverrideList(string functionName)
        {
            List<OverrideMethods> res;
            Overrides.TryGetValue(functionName, out res);
            return res;
        }
    }
}