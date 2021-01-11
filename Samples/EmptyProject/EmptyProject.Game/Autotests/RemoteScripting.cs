using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Lime;
using RemoteScripting;

#if iOS
using UIKit;
#endif

namespace EmptyProject
{
	public class RemoteScripting
	{
		private const int RenderChainLayer = RenderChain.LayerCount - 1;
		private const string ScenePath = "Shell/RemoteScripting";
		private const string EntryPointsClassName = "EmptyProject.Scripts.EntryPoints";

		private enum StatusMessage
		{
			None,
			Connecting,
			Connected,
			Disconnected,
			Failed
		}

		private class RemoteFileReceiver
		{
			public string Path;
			public RemoteFile RemoteFile;
		}

		private static readonly Dictionary<string, IPAddress> serversAddresses = new Dictionary<string, IPAddress> {
			{ "Localhost", IPAddress.Parse("127.0.0.1") },
		};
		private static RemoteScripting instance;
		private static readonly object statusTaskTag = new object();
		private static readonly List<RemoteFileReceiver> remoteFileReceivers = new List<RemoteFileReceiver>();
		private static bool IsReadyToConnect => instance == null;

		private readonly Client client;
		private readonly Frame root;
		private bool wasDisconnected;

		public static bool IsConnected() => instance != null && !instance.wasDisconnected && instance.client.IsConnected && !instance.client.WasFailed;

		public static readonly object ScriptsTasksTag = new object();
		public static Frame Frame => instance?.root;

		public RemoteScripting(string hostName) : this(serversAddresses[hostName]) { }

		public RemoteScripting(IPAddress ipAddress)
		{
			root = Node.Load<Frame>(ScenePath);
			root.PushToNode(The.World);
			root.Layer = RenderChainLayer;
			root.ExpandToContainerWithAnchors();
			root.RunAnimation("Def");

			client = new Client();
			The.World.Tasks.Add(ConnectionTask(ipAddress));
			instance = this;
		}

		private IEnumerator<object> ConnectionTask(IPAddress ipAddress)
		{
			SetStatusMessage(StatusMessage.Connecting);
			client.Connect(ipAddress);
			yield return Task.WaitWhile(() => !client.IsConnected && !client.WasFailed);

			bool CheckForErrors()
			{
				if (!client.WasFailed || client.FailException is System.Threading.Tasks.TaskCanceledException) {
					return false;
				}
				if (client.FailException != null) {
					Logger.Instance.Error(@"Remote Scripting: Exception while processing connection: {0}", client.FailException);
				} else {
					Logger.Instance.Error(@"Remote Scripting: Unknown exception while processing connection");
				}
				SetStatusMessage(StatusMessage.Failed);
				instance = null;
				return true;
			}

			if (CheckForErrors()) {
				yield break;
			}
			SetStatusMessage(StatusMessage.Connected);

			SendDeviceName();
			ConsoleInterception consoleInterception = null;
			try {
				consoleInterception = new ConsoleInterception();
				consoleInterception.OnWrite += str => {
					if (IsConnected()) {
						var message = new NetworkText(str);
						client.SendMessage(message);
					}
				};

				while (IsConnected()) {
					HandleServerMessages();
					yield return null;
				}
			} finally {
				try {
					client.Close();
				} catch {
					// Suppress
				}

				consoleInterception?.Close();
				if (!CheckForErrors()) {
					SetStatusMessage(StatusMessage.Disconnected);
				}
				instance = null;

				IEnumerator<object> UnlinkAndDisposeTask()
				{
					yield return Task.WaitWhile(() => root.Tasks.Count > 1);
					root.UnlinkAndDispose();
				}
				root.Tasks.Add(UnlinkAndDisposeTask);
			}
		}

		private void SendDeviceName()
		{
			var deviceName = GetDeviceName();
			client.SendMessage(new NetworkDeviceName(deviceName));
		}

		public static string GetDeviceName()
		{
#if WIN
			var deviceName = System.Environment.MachineName.ToLowerInvariant();
#elif iOS
			var deviceName = UIDevice.CurrentDevice.Name;
#elif ANDROID
			var manufacturer = Android.OS.Build.Manufacturer;
			var model = Android.OS.Build.Model;
			var isModelIncludeManufacturer = model.ToLowerInvariant().StartsWith(manufacturer.ToLowerInvariant(), StringComparison.InvariantCulture);
			var deviceName = isModelIncludeManufacturer ? model : $"{manufacturer} {model}";
#else
			var deviceName = "Undefined";
#endif
#if iOS || ANDROID
			deviceName += $" ({The.Application.GetVersion()})";
#endif
			return deviceName;
		}

		private void HandleServerMessages()
		{
			while (client.TryReceiveMessage(out var message)) {
				switch (message.MessageType) {
					case NetworkMessageType.RemoteProcedureCall:
						var remoteProcedureCall = ((NetworkRemoteProcedureCall)message).Data;
						try {
							var portableAssembly = new PortableAssembly(remoteProcedureCall.AssemblyRawBytes, remoteProcedureCall.PdbRawBytes, EntryPointsClassName);
							portableAssembly.EntryPoints
								.First(p => p.ClassName == remoteProcedureCall.ClassName && p.MethodName == remoteProcedureCall.MethodName)
								.Info
								.Invoke(null, null);
						} catch (System.Exception e) {
							Logger.Instance.Info(e.ToString());
						}
						break;
					case NetworkMessageType.RemoteFile:
						var remoteFile = ((NetworkRemoteFile)message).Data;
						foreach (var remoteFileReceiver in remoteFileReceivers) {
							if (remoteFileReceiver.Path == remoteFile.Path) {
								remoteFileReceiver.RemoteFile = remoteFile;
								break;
							}
						}
						break;
					default:
						throw new NotSupportedException();
				}
			}
		}

		public static IEnumerator<object> RequestRemoteFileTask(string path, Lime.TaskResult<byte[]> result)
		{
			if (!IsConnected()) {
				yield break;
			}
			var fileRequest = new RemoteFileRequest { Path = path };
			instance.client.SendMessage(new NetworkRemoteFileRequest(fileRequest));
			var remoteFileReceiver = new RemoteFileReceiver { Path = path };
			try {
				remoteFileReceivers.Add(remoteFileReceiver);
				yield return Task.WaitWhile(() => IsConnected() && remoteFileReceiver.RemoteFile == null);
				result.Value = remoteFileReceiver.RemoteFile?.Bytes;
			} finally {
				remoteFileReceivers.Remove(remoteFileReceiver);
			}
		}

		public static void SendRemoteFile(string path, byte[] bytes)
		{
			var remoteFile = new RemoteFile {
				Path = path,
				Bytes = bytes,
			};
			instance.client.SendMessage(new NetworkRemoteFile(remoteFile));
		}

		private static void Disconnect()
		{
			if (instance != null) {
				instance.wasDisconnected = true;
			}
		}

		private void SetStatusMessage(StatusMessage statusMessage)
		{
			root.Tasks.StopByTag(statusTaskTag);

			var statusBar = root["StatusBar"];
			statusBar.RunAnimation(statusMessage.ToString());
			var presentation = statusBar.Animations.Find("Presentation");
			presentation.Run("ShowAndHide");

			IEnumerator<object> StatusMessageTask()
			{
				yield return Task.WaitWhile(() => presentation.IsRunning);
			}
			root.Tasks.Add(StatusMessageTask, statusTaskTag);
		}

		public static void FillDebugMenuItems(RainbowDash.Menu menu)
		{
			var section = menu.Section("Remote Scripting");
			if (IsReadyToConnect) {
				foreach (var (serverTitle, ipAddress) in serversAddresses) {
					section.Item(serverTitle, () => new RemoteScripting(ipAddress));
				}
			} else {
				section.Item("Disconnect", Disconnect);
			}
		}

		public class ConsoleInterception : TextWriter
		{
			private readonly TextWriter originalConsoleWriter = Console.Out;

			public override Encoding Encoding => originalConsoleWriter.Encoding;
			public event Action<string> OnWrite;

			public ConsoleInterception()
			{
				Console.SetOut(this);
			}

			public override void Write(char value)
			{
				originalConsoleWriter.Write(value);
			}

			public override void Flush()
			{
				originalConsoleWriter.Flush();
			}

			public override void Write(string value)
			{
				originalConsoleWriter.Write(value);
				OnWrite?.Invoke(value);
			}

			public override void WriteLine(string value)
			{
				originalConsoleWriter.WriteLine(value);
				OnWrite?.Invoke(value);
			}
		}
	}

	public static class ScriptingToolbox
	{
		public static TaskList PreEarlyTasks => The.World.Components.GetOrAdd<PreEarlyStageBehavior>().Tasks;
		public static TaskList PostLateTasks => The.World.Components.GetOrAdd<PostLateStageBehavior>().Tasks;
		public static RenderChain WorldRenderChain { get; set; }

		private static readonly System.Reflection.FieldInfo requiredToWaitForWindowRenderingField =
			typeof(Application.Application).GetField("requiredToWaitForWindowRendering", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		private static bool RequiredToWaitForWindowRendering => (bool)requiredToWaitForWindowRenderingField.GetValue(The.App);

		public static void CustomWorldUpdating(float delta, int iterationsCount, bool isTimeQuantized, Action<float, bool> updateFrameAction)
		{
			void UpdateFrame(float d, bool requiredInputSimulation)
			{
				updateFrameAction(d, false);
				if (!RequiredToWaitForWindowRendering && requiredInputSimulation) {
					Lime.Application.Input.Simulator.OnBetweenFrames(delta);
				}
			}

			if (iterationsCount == 1) {
				var validDelta = Mathf.Clamp(delta, 0, Lime.Application.MaxDelta);
				iterationsCount = validDelta >= Mathf.ZeroTolerance ? (int)(delta / validDelta) : 1;
				var remainDelta = delta - validDelta * iterationsCount;
				for (var i = 0; i < iterationsCount; i++) {
					UpdateFrame(validDelta, i + 1 < iterationsCount || remainDelta > 0);
					if (RequiredToWaitForWindowRendering) {
						break;
					}
				}
				if (remainDelta > 0 && !isTimeQuantized && !RequiredToWaitForWindowRendering) {
					UpdateFrame(remainDelta, false);
				}
			} else {
				for (var i = 0; i < iterationsCount; i++) {
					UpdateFrame(delta, i + 1 < iterationsCount);
					if (RequiredToWaitForWindowRendering) {
						break;
					}
				}
			}

			if (WorldRenderChain != null) {
				The.World.AddToRenderChain(WorldRenderChain);
			}
		}

		[NodeComponentDontSerialize]
		[UpdateStage(typeof(PreEarlyUpdateStage))]
		public class PreEarlyStageBehavior : BehaviorComponent
		{
			public TaskList Tasks { get; private set; }

			protected override void OnOwnerChanged(Node oldOwner)
			{
				Tasks?.Stop();
				Tasks = Owner == null ? null : new TaskList(Owner);
			}

			protected override void Update(float delta) => Tasks.Update(delta);

			public override void Dispose() => Tasks?.Stop();
		}

		[NodeComponentDontSerialize]
		[UpdateStage(typeof(PostLateUpdateStage))]
		public class PostLateStageBehavior : BehaviorComponent
		{
			public TaskList Tasks { get; private set; }

			protected override void OnOwnerChanged(Node oldOwner)
			{
				Tasks?.Stop();
				Tasks = Owner == null ? null : new TaskList(Owner);
			}

			protected override void Update(float delta) => Tasks.Update(delta);

			public override void Dispose() => Tasks?.Stop();
		}
	}
}
