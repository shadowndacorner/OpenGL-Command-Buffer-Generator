using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen.Overrides
{
    public class ShaderOverrides : GLOverride
    {
        public const string DataBuffer = CodeGenerator.DataBuffer;
        public const string ResourceManager = CodeGenerator.ResourceManager;
        public const string ResourceType = "Shader";
        public const string Resources = ResourceManager + "." + ResourceType + "s";
        public const string ResourceHandleType = ResourceType + "Handle";

        public override void InitializeOverrides(CodegenOverrideTracker overrides)
        {
            overrides.RegisterArgumentTypeReadOverride(ResourceHandleType, async (context, function, arg) =>
            {
                await context.EmitLine($"{ResourceHandleType} {arg.Name}Handle = {DataBuffer}.read<{ResourceHandleType}>();");
                await context.EmitLine($"auto& {arg.Name} = *{Resources}.get({arg.Name}Handle);");
            });

            overrides.RegisterOverride("glCreateShader",
                overrideEntry: (fn) => {
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& target = *{Resources}.get(handle);");
                    await EmitArgumentReads(context, entry);
                    await context.EmitLine($"target = glCreateShader({GenerateTypelessArgumentList(entry)});");
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {Resources}.create(0);");
                    await context.EmitLine($"{DataBuffer}.write(handle);");
                    await EmitArgumentWrites(context, entry);

                    await context.EmitLine($"return handle;");
                }
            );

            overrides.RegisterOverride("glShaderSource",
                overrideEntry: (fn) => {
                    var args = fn.Type.Arguments;
                    args[0].Type = ResourceHandleType;
                    args.RemoveAt(1);
                    args[1].Type = "const GLchar*";
                    args[2].Type = "GLint";
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    var args = entry.Type.Arguments;
                    await defaultRead();
                    await context.EmitLine($"glShaderSource({args[0].Name}, 1, &{args[1].Name}, &{args[2].Name});");
                }
            );

            foreach (var kv in overrides.Parser.Functions)
            {
                foreach (var arg in kv.Value.Type.Arguments)
                {
                    if (arg.Name == "shader")
                    {
                        arg.Type = ResourceHandleType;
                    }
                }
            }
        }
    }
}