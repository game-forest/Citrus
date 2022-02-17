using System;
using System.IO;
using System.Text;
using Lime;
using RemoteScripting;
using Tangerine.Core;

namespace Tangerine.UI.RemoteScripting
{
	public class RemoteDevice : ExplorableItem
	{
		private static readonly char[] invalidFilenameChars;

		private readonly LimitedTextView textView;

		public HostClient Client { get; }
		public bool WasInitialized { get; private set; }
		public bool WasDisconnected { get; private set; }

		static RemoteDevice()
		{
			invalidFilenameChars = Path.GetInvalidFileNameChars();
			Array.Sort(invalidFilenameChars);
		}

		public RemoteDevice(HostClient client)
		{
			Client = client;
			Name = Guid.NewGuid().ToString().Substring(0, 18);
			UpdateVisual();

			Content = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(textView = new LimitedTextView(maxRowCount: 1500)),
				},
			};
		}

		public void Disconnect()
		{
			Client.Close();
			WasDisconnected = true;
			UpdateVisual();
		}

		public void RemoteProcedureCall(
			byte[] assemblyRawBytes, byte[] pdbRawBytes, string className, string methodName
		) {
			var remoteProcedureCall = new RemoteProcedureCall {
				AssemblyRawBytes = assemblyRawBytes,
				PdbRawBytes = pdbRawBytes,
				ClassName = className,
				MethodName = methodName,
			};
			Client.SendMessage(new NetworkRemoteProcedureCall(remoteProcedureCall));
		}

		public void SetOutputDirectory(string directory)
		{
			if (directory == null) {
				textView.CloseFile();
				return;
			}

			var fileName = ToValidFilename($"{DateTime.Now:yyyy.MM.dd H-mm-ss} {Name}.txt");
			textView.FilePath = !string.IsNullOrEmpty(directory) ? Path.Combine(directory, fileName) : null;

			static string ToValidFilename(string input)
			{
				var result = new StringBuilder(input.Length);
				foreach (var @char in input) {
					if (Array.BinarySearch(invalidFilenameChars, @char) < 0) {
						result.Append(@char);
					}
				}
				return result.ToString();
			}
		}

		public void ProcessMessages()
		{
			while (Client.TryReceiveMessage(out var message)) {
				switch (message.MessageType) {
					case NetworkMessageType.DeviceName:
						var networkDeviceName = (NetworkDeviceName)message;
						Name = networkDeviceName.Name;
						WasInitialized = true;
						UpdateVisual();
						break;
					case NetworkMessageType.Text:
						var networkText = (NetworkText)message;
						Log(networkText.Text);
						break;
					case NetworkMessageType.RemoteFileRequest: {
							var fileRequest = (NetworkRemoteFileRequest)message;
							Log($"Remote file request: \"{fileRequest.Data.Path}\"");
							var remoteStoragePath =
								ProjectPreferences.Instance.RemoteScriptingCurrentConfiguration?.RemoteStoragePath;
							if (!string.IsNullOrEmpty(remoteStoragePath)) {
								var absFilePath = Path.Combine(remoteStoragePath, fileRequest.Data.Path);
								if (File.Exists(absFilePath)) {
									SendFileAsync(absFilePath, fileRequest.Data.Path);
									break;
								}
							}

							var remoteFile = new RemoteFile {
								Path = fileRequest.Data.Path,
								Bytes = null,
							};
							Client.SendMessage(new NetworkRemoteFile(remoteFile));
							Log(
								string.IsNullOrEmpty(remoteStoragePath)
									? "Can not send requested file: "
										+ "Please, setup remote storage path in project configuration file!"
									: $"Can not send requested file: \"{fileRequest.Data.Path}\". File not found!"
							);
							break;
						}
					case NetworkMessageType.RemoteFile: {
							var remoteFile = ((NetworkRemoteFile)message).Data;
							var remoteStoragePath = ProjectPreferences.Instance
								.RemoteScriptingCurrentConfiguration?.RemoteStoragePath;
							if (remoteFile.Bytes == null || remoteFile.Bytes.Length == 0) {
								Log($"Can not receive remote file \"{remoteFile.Path}\". File is empty.");
							} else if (string.IsNullOrEmpty(remoteStoragePath)) {
								Log(
									$"Can not receive remote file \"{remoteFile.Path}\". " +
									$"Please, setup remote storage path in project configuration file!"
								);
							} else {
								var filePath = Path.Combine(remoteStoragePath, remoteFile.Path);
								SaveFileAsync(filePath, remoteFile);
							}
							break;
						}
					default:
						throw new NotSupportedException($"Unknown message type: {message.MessageType}");
				}
			}

			async void SendFileAsync(string filePath, string requestPath)
			{
				byte[] bytes;
				await using (var fileStream = File.Open(filePath, FileMode.Open)) {
					bytes = new byte[fileStream.Length];
					await fileStream.ReadAsync(bytes, 0, (int)fileStream.Length);
				}
				var remoteFile = new RemoteFile {
					Path = requestPath,
					Bytes = bytes,
				};
				Client.SendMessage(new NetworkRemoteFile(remoteFile));
				Log($"Requested file \"{requestPath}\" was sended.");
			}

			async void SaveFileAsync(string filePath, RemoteFile remoteFile)
			{
				try {
					var directory = Path.GetDirectoryName(filePath);
					Directory.CreateDirectory(directory!);
					await using (var fileStream = File.OpenWrite(filePath)) {
						await fileStream.WriteAsync(remoteFile.Bytes, 0, remoteFile.Bytes.Length);
					}
					Log($"Remote file \"{remoteFile.Path}\" was recieved.");
				} catch (System.Exception exception) {
					Log($"Unhandled extention while saving remote file \"{remoteFile.Path}\".\n{exception}");
				}
			}
		}

		private void UpdateVisual()
		{
			IconTexture = IconPool.GetTexture(
				!WasDisconnected ?
				"RemoteScripting.DeviceConnected" :
				"RemoteScripting.DeviceDisconnected"
			);
		}

		private void Log(string message)
		{
			if (message != null) {
				textView.AppendLine(
					!string.IsNullOrWhiteSpace(message) ? $"[{DateTime.Now:dd.MM.yy H:mm:ss}] {message}" : message
				);
			}
		}
	}
}
