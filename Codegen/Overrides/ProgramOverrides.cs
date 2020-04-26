using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen.Overrides
{
    public class ProgramOverrides : GLOverride
    {
        public const string DataBuffer = CodeGenerator.DataBuffer;
        public const string ResourceManager = CodeGenerator.ResourceManager;
        public const string ResourceType = "ShaderProgram";
        public const string Resources = ResourceManager + "." + ResourceType + "s";
        public const string ResourceHandleType = ResourceType + "Handle";

        public override void InitializeOverrides(CodegenOverrideTracker overrides)
        {
            overrides.RegisterArgumentTypeReadOverride(ResourceHandleType, async (context, function, arg) =>
            {
                await context.EmitLine($"{ResourceHandleType} {arg.Name}Handle = {DataBuffer}.read<{ResourceHandleType}>();");
                await context.EmitLine($"auto& {arg.Name} = *{Resources}.get({arg.Name}Handle);");
            });

            overrides.RegisterOverride("glCreateProgram",
                overrideEntry: (fn) => {
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& target = *{Resources}.get(handle);");
                    await context.EmitLine($"target = glCreateProgram();");
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {Resources}.create();");
                    await context.EmitLine($"{DataBuffer}.write(handle);");
                    await context.EmitLine($"return handle;");
                }
            );

            overrides.RegisterOverride("glUseProgram",
                overrideEntry: (fn) => {
                    fn.Type.Arguments[0].Type = ResourceHandleType;
                }
            );

            overrides.RegisterOverride("glAttachShader",
                overrideEntry: (fn) => {
                    fn.Type.Arguments[0].Type = ResourceHandleType;
                    fn.Type.Arguments[0].Type = ShaderOverrides.ResourceHandleType;
                }
            );

            foreach (var kv in overrides.Parser.Functions)
            {
                if (kv.Key.StartsWith("glProgramUniform"))
                {
                    kv.Value.Type.Arguments[0].Type = ResourceHandleType;
                }

                foreach(var arg in kv.Value.Type.Arguments)
                {
                    if (arg.Name == "program")
                    {
                        arg.Type = ResourceHandleType;
                    }
                }
            }
        }
    }
}