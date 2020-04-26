using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen.Overrides
{
    public class VertexArrayrOverrides : GLOverride
    {
        public const string DataBuffer = CodeGenerator.DataBuffer;
        public const string ResourceManager = CodeGenerator.ResourceManager;
        public const string ResourceType = "VertexArray";
        public const string Resources = ResourceManager + "." + ResourceType + "s";
        public const string ResourceHandleType = ResourceType + "Handle";

        public override void InitializeOverrides(CodegenOverrideTracker overrides)
        {
            overrides.RegisterArgumentTypeReadOverride(ResourceHandleType, async (context, function, arg) =>
            {
                await context.EmitLine($"{ResourceHandleType} {arg.Name}Handle = {DataBuffer}.read<{ResourceHandleType}>();");
                await context.EmitLine($"auto& {arg.Name} = *{Resources}.get({arg.Name}Handle);");
            });

            overrides.RegisterOverride($"glCreate{ResourceType}s",
                overrideEntry: (fn) => {
                    fn.Name = "glCreate" + ResourceType;
                    fn.Type.Arguments.Clear();
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& target = *{Resources}.get(handle);");
                    await context.EmitLine($"glCreate{ResourceType}s(1, &target);");
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {Resources}.create(0);");
                    await context.EmitLine($"{DataBuffer}.write<{ResourceHandleType}>(handle);");
                    await context.EmitLine($"return handle;");
                }
            );

            overrides.RegisterOverride($"glGen{ResourceType}s",
                overrideEntry: (fn) => {
                    fn.Name = "glGen" + ResourceType;
                    fn.Type.Arguments.Clear();
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& target = *{Resources}.get(handle);");
                    await context.EmitLine($"glGen{ResourceType}s(1, &target);");
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {Resources}.create(0);");
                    await context.EmitLine($"{DataBuffer}.write<{ResourceHandleType}>(handle);");
                    await context.EmitLine($"return handle;");
                }
            );
            
            overrides.RegisterOverride($"glBindVertexArray",
                overrideEntry: (fn) => {
                    fn.Type.Arguments[0].Type = ResourceHandleType;
                }
            );

            foreach (var kv in overrides.Parser.Functions)
            {
                foreach (var arg in kv.Value.Type.Arguments)
                {
                    if (arg.Name == "vaobj")
                    {
                        arg.Type = ResourceHandleType;
                    }
                }
            }
        }
    }
}