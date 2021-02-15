using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
#if WIN
using System.Runtime.InteropServices;
#endif // WIN

namespace Orange
{
	public static class Process
	{
#if WIN
		[DllImport("kernel32.dll")]
		static extern uint GetOEMCP();
#endif // WIN

		[Flags]
		public enum Options
		{
			None = 0,
			RedirectOutput = 1,
			RedirectErrors = 2,
			All = 3
		}

		public static int Start(
			string executablePath,
			string arguments,
			string workingDirectory = null,
			Options options = Options.RedirectOutput | Options.RedirectErrors,
			StringBuilder output = null
		) {
			if (!string.IsNullOrEmpty(workingDirectory)) {
				Console.WriteLine($"Working directory: '{workingDirectory}'");
			}
			Console.WriteLine($"Starting '{executablePath} {arguments}'");
			if (output == null) {
				output = new StringBuilder();
			}
			var p = new System.Diagnostics.Process();
			p.StartInfo.FileName = executablePath;
			p.StartInfo.Arguments = arguments;
			p.StartInfo.UseShellExecute = false;
			var encoding = System.Text.Encoding.Default;
#if WIN
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath);
			int cp = (int)GetOEMCP();
			encoding = CodePagesEncodingProvider.Instance.GetEncoding(cp) ?? encoding;
#else // WIN
			if (workingDirectory != null) {
				p.StartInfo.WorkingDirectory = workingDirectory;
			}
#endif // WIN
			p.StartInfo.StandardOutputEncoding = encoding;
			p.StartInfo.StandardErrorEncoding = encoding;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.RedirectStandardError = true;
			var logger = new System.Text.StringBuilder();
			if ((options & Options.RedirectOutput) != 0) {
				p.OutputDataReceived += (sender, e) => {
					lock (logger) {
						if (e.Data != null) {
							logger.AppendLine(e.Data);
							output.AppendLine(e.Data);
						}
					}
				};
			}
			if ((options & Options.RedirectErrors) != 0) {
				p.ErrorDataReceived += (sender, e) => {
					lock (logger) {
						if (e.Data != null) {
							logger.AppendLine(e.Data);
							output.AppendLine(e.Data);
						}
					}
				};
			}
			p.Start();
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();
			while (!p.HasExited) {
				p.WaitForExit(50);
				lock (logger) {
					if (logger.Length > 0) {
						Console.Write(logger.ToString());
						logger.Clear();
					}
				}
				The.UI.ProcessPendingEvents();
			}
			// WaitForExit WITHOUT timeout argument waits for async output and error streams to finish
			p.WaitForExit();
			var exitCode = p.ExitCode;
			p.Close();
			Console.Write(logger.ToString());
			return exitCode;
		}
	}
}
