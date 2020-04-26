using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GLThreadGen
{
    public class CodegenContext
    {
        public CodegenContext(FileStream stream)
        {
            if (!stream.CanWrite)
            {
                throw new ArgumentException("Code generation requires a writeable stream");
            }
            Stream = stream;
        }

        public FileStream Stream { get; private set; }
        private int _indent = 0;
        
        public int IndentLevel
        {
            get
            {
                return _indent;
            }

            set
            {
                if (value < 0)
                    _indent = 0;
                else
                    _indent = value;
            }
        }

        public async Task Emit(string code)
        {
            var byteArray = Encoding.ASCII.GetBytes(code);
            await Stream.WriteAsync(byteArray, 0, byteArray.Length);
        }

        public void EmitLine()
        {
            Stream.WriteByte((byte)'\n');
        }

        public void EmitIndent()
        {
            for (int i = 0; i < IndentLevel; ++i)
            {
                Stream.WriteByte((byte)'\t');
            }
        }

        public async Task EmitLine(string line)
        {
            EmitIndent();
            await EmitLineUnindented(line);
        }

        public async Task EmitLineUnindented(string line)
        {
            var byteArray = Encoding.ASCII.GetBytes(line);
            await Stream.WriteAsync(byteArray, 0, byteArray.Length);
            Stream.WriteByte((byte)'\n');
        }
    }

    public static class CodeGenCPPExtensions
    {
        public static async Task EmitStructAccess(this CodegenContext context, string access)
        {
            if (!access.EndsWith(':'))
                access = access + ":";

            --context.IndentLevel;
            await context.EmitLine(access);
            ++context.IndentLevel;
        }
    }
}
