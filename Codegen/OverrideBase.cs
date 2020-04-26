using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen.Overrides
{
    public class GLOverride
    {
        public virtual void InitializeOverrides(CodegenOverrideTracker overrides) { }
        public string GenerateTypelessArgumentList(FunctionEntry function)
        {
            StringBuilder build = new StringBuilder();
            var args = function.Type.Arguments;
            for (int i = 0; i < args.Count; ++i)
            {
                var arg = args[i];
                build.Append($"{arg.Name}");
                if (i < args.Count - 1)
                {
                    build.Append(", ");
                }
            }
            return build.ToString();
        }

        public async Task EmitArgumentReads(CodegenContext context, FunctionEntry function)
        {
            var args = function.Type.Arguments;
            for (int i = 0; i < args.Count; ++i)
            {
                var arg = args[i];
                await context.EmitLine($"{arg.Type} {arg.Name} = {CodeGenerator.DataBuffer}.read<{arg.Type}>();");
            }
        }

        public async Task EmitArgumentWrites(CodegenContext context, FunctionEntry function)
        {
            var args = function.Type.Arguments;
            for (int i = 0; i < args.Count; ++i)
            {
                var arg = args[i];
                await context.EmitLine($"{CodeGenerator.DataBuffer}.write<{arg.Type}>({arg.Name});");
            }
        }
    }
}