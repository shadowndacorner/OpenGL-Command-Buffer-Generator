using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen.Overrides
{
    public class BufferOverrides : GLOverride
    {
        public const string DataBuffer = CodeGenerator.DataBuffer;
        public const string ResourceManager = CodeGenerator.ResourceManager;
        public const string ResourceType = "Buffer";
        public const string Resources = ResourceManager + "." + ResourceType + "s";
        public const string ResourceHandleType = ResourceType + "Handle";

        public override void InitializeOverrides(CodegenOverrideTracker overrides)
        {
            overrides.RegisterArgumentTypeReadOverride(ResourceHandleType, async (context, function, arg) =>
            {
                await context.EmitLine($"{ResourceHandleType} {arg.Name}Handle = {DataBuffer}.read<{ResourceHandleType}>();");
                await context.EmitLine($"auto& {arg.Name} = *{Resources}.get({arg.Name}Handle);");
            });

            overrides.RegisterOverride("glCreateBuffers",
                overrideEntry: (fn) => {
                    fn.Name = "glCreateBuffer";
                    fn.Type.Arguments.Clear();
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& target = *{Resources}.get(handle);");
                    await context.EmitLine($"glCreateBuffers(1, &target);");
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {Resources}.create();");
                    await context.EmitLine($"m_Buffer.write<{ResourceHandleType}>(handle);");
                    await context.EmitLine($"return handle;");
                }
            );

            overrides.RegisterOverride("glGenBuffers",
                overrideEntry: (fn) => {
                    fn.Name = "glGenBuffer";
                    fn.Type.Arguments.Clear();
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& target = *{Resources}.get(handle);");
                    await context.EmitLine($"glGenBuffers(1, &target);");
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {ResourceManager}.Buffers.create();");
                    await context.EmitLine($"m_Buffer.write<{ResourceHandleType}>(handle);");
                    await context.EmitLine($"return handle;");
                }
            );

            foreach (var kv in overrides.Parser.Functions)
            {
                foreach (var arg in kv.Value.Type.Arguments)
                {
                    if (arg.Name == "buffer")
                    {
                        arg.Type = ResourceHandleType;
                    }
                }
            }
        }
    }
}