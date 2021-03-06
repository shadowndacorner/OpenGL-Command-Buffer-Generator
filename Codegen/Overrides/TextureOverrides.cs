using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen.Overrides
{
    public class TextureOverrides : GLOverride
    {
        public const string DataBuffer = CodeGenerator.DataBuffer;
        public const string ResourceManager = CodeGenerator.ResourceManager;
        public const string ResourceType = "Texture";
        public const string Resources = ResourceManager + "." + ResourceType + "s";
        public const string ResourceHandleType = ResourceType + "Handle";

        public override void InitializeOverrides(CodegenOverrideTracker overrides)
        {
            overrides.RegisterArgumentTypeReadOverride(ResourceHandleType, async (context, function, arg) =>
            {
                await context.EmitLine($"{ResourceHandleType} {arg.Name}Handle = {DataBuffer}.read<{ResourceHandleType}>();");
                await context.EmitLine($"auto& {arg.Name} = *{Resources}.get({arg.Name}Handle);");
            });

            overrides.RegisterOverride("glCreateTextures",
                overrideEntry: (fn) => {
                    fn.Name = "glCreateTexture";
                    
                    var args = fn.Type.Arguments;
                    args.RemoveRange(1, 2);
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    var arg = entry.Type.Arguments[0];
                    await context.EmitLine($"{DataBuffer}.write({arg.Name});");
                    await context.EmitLine($"auto handle = {Resources}.create(0);");
                    await context.EmitLine($"{DataBuffer}.write(handle);");
                    await context.EmitLine($"return handle;");
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    var arg = entry.Type.Arguments[0];
                    await context.EmitLine($"auto {arg.Name} = {DataBuffer}.read<{arg.Type}>();");
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& {ResourceType.ToLower()} = *{Resources}.get(handle);");
                    await context.EmitLine($"glCreateTextures({arg .Name}, 1, &{ResourceType.ToLower()});");
                }
            );
            
            overrides.RegisterOverride("glGenTextures",
                overrideEntry: (fn) => {
                    fn.Name = "glGenTexture";
                    
                    var args = fn.Type.Arguments;
                    args.Clear();
                    fn.Type.ReturnType = ResourceHandleType;
                },
                modifyWriteFunc: async (context, defaultWrite, entry) => {
                    context.EmitLine();
                    await context.EmitLine($"auto handle = {Resources}.create(0);");
                    await context.EmitLine($"{DataBuffer}.write(handle);");
                    await context.EmitLine($"return handle;");
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await context.EmitLine($"auto handle = {DataBuffer}.read<{ResourceHandleType}>();");
                    await context.EmitLine($"auto& {ResourceType.ToLower()} = *{Resources}.get(handle);");
                    await context.EmitLine($"glGenTextures(1, &{ResourceType.ToLower()});");
                }
            );

            overrides.RegisterOverride("glActiveTexture",
                overrideEntry: (fn) => {
                    var args = fn.Type.Arguments;
                    args[0].Name = "textureSlot";
                    args[0].Type = "GLuint";
                    args[0].IsEnumType = false;
                },
                modifyReadFunc: async (context, defaultRead, entry) => {
                    await defaultRead();
                    await context.EmitLine("GL_CHECK(glActiveTexture(GL_TEXTURE0 + textureSlot));");
                }
            );

            foreach (var kv in overrides.Registry.Functions)
            {
                foreach (var arg in kv.Value.Type.Arguments)
                {
                    if (arg.Name == "texture")
                    {
                        arg.Type = ResourceHandleType;
                    }
                }
            }
        }
    }
}