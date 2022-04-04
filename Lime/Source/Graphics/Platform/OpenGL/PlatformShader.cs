using System.Text.RegularExpressions;
#if !iOS && !MAC && !ANDROID
using OpenTK.Graphics.ES20;
#endif

namespace Lime.Graphics.Platform.OpenGL
{
	internal class PlatformShader : IPlatformShader
	{
		/// <summary>
		/// Matches default precision qualifiers and precision qualifier for type specifiers.
		/// </summary>
		private static readonly Regex precisionQualifiers = new Regex(
			pattern: @"lowp |mediump |highp |precision \w+ \w+;", RegexOptions.Compiled);

		internal int GLShader;

		public PlatformRenderContext Context { get; }
		public ShaderStageMask Stage { get; }

		internal PlatformShader(PlatformRenderContext context, ShaderStageMask stage, string source)
		{
			Context = context;
			Stage = stage;
			Initialize(source);
		}

		public void Dispose()
		{
			if (GLShader != 0) {
				GL.DeleteShader(GLShader);
				GLHelper.CheckGLErrors();
				GLShader = 0;
			}
		}

		private void Initialize(string source)
		{
			GLShader = GL.CreateShader(GLHelper.GetGLShaderType(Stage));
			GLHelper.CheckGLErrors();
			GL.ShaderSource(GLShader, ProcessSource(source));
			GLHelper.CheckGLErrors();
			GL.CompileShader(GLShader);
			GLHelper.CheckGLErrors();
			GL.GetShader(GLShader, ShaderParameter.CompileStatus, out int compileStatus);
			if (compileStatus == 0) {
				var infoLog = GL.GetShaderInfoLog(GLShader);
				GLHelper.CheckGLErrors();
				GL.DeleteShader(GLShader);
				GLHelper.CheckGLErrors();
				throw new System.Exception($"Shader compilation failed:\n{infoLog}");
			}
		}

		private string ProcessSource(string source)
		{
			if (!Context.ESProfile) {
				source = precisionQualifiers.Replace(source, string.Empty);
			}
			return source;
		}
	}
}
