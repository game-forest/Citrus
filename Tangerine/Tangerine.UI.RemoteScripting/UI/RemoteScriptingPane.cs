using System.Collections.Generic;
using System.IO;
using Lime;
using RemoteScripting;
using Tangerine.Core;
using Tangerine.UI.Docking;

namespace Tangerine.UI.RemoteScripting
{
	public class RemoteScriptingPane
	{
		private static bool AutoStartUpHosting
		{
			get => ProjectUserPreferences.Instance.RemoteScriptingAutoStartHosting;
			set => ProjectUserPreferences.Instance.RemoteScriptingAutoStartHosting = value;
		}

		private static bool AutoRebuildAssembly
		{
			get => ProjectUserPreferences.Instance.RemoteScriptingAutoRebuildAssembly;
			set => ProjectUserPreferences.Instance.RemoteScriptingAutoRebuildAssembly = value;
		}

		private static string DeviceLogsDirectory
		{
			get => ProjectUserPreferences.Instance.RemoteDevicesLogFolder;
			set => ProjectUserPreferences.Instance.RemoteDevicesLogFolder = value;
		}

		private static bool IsSavingDeviceLogs => !string.IsNullOrEmpty(DeviceLogsDirectory);

		private readonly Panel panel;
		private readonly Dictionary<HostClient, RemoteDevice> devices = new Dictionary<HostClient, RemoteDevice>();
		private Host host;
		private Widget rootWidget;
		private ExplorableScrollView explorableItems;

		public RemoteScriptingPane(Panel panel)
		{
			this.panel = panel;
			panel.ContentWidget.AddChangeWatcher(() => Project.Current.CitprojPath, path => Initialize());
		}

		private void Initialize()
		{
			CleanUp();
			InitializeWidgets();
		}

		private void InitializeWidgets()
		{
			if (DeviceLogsDirectory != null && !Directory.Exists(DeviceLogsDirectory)) {
				DeviceLogsDirectory = null;
			}

			Toolbar toolbar;
			RemoteProcedureCallScrollView rpcScrollView;
			rootWidget = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(toolbar = new Toolbar()),
					new ThemedHSplitter {
						SeparatorWidth = 1f,
						Padding = new Thickness(5f),
						Stretches = {0.15f, 0.15f, 0.7f},
						Nodes = {
							(explorableItems = new ExplorableScrollView {
								MinWidth = 150,
								TabTravesable = new TabTraversable(),
								CompoundPresenter = {
									new ThemedFramePresenter(Theme.Colors.WhiteBackground, Theme.Colors.ControlBorder)
								},
								Content = { Layout = new VBoxLayout() },
							}),
							(rpcScrollView = new RemoteProcedureCallScrollView {
								MinWidth = 150,
								TabTravesable = new TabTraversable(),
								CompoundPresenter = {
									new ThemedFramePresenter(Theme.Colors.WhiteBackground, Theme.Colors.ControlBorder)
								},
								Content = {
									Layout = new VBoxLayout { Spacing = 2f },
								},
							}),
							(explorableItems.ExploringWidget = new Widget {
								MinWidth = 300,
								Layout = new HBoxLayout(),
							})
						}
					}
				}
			};
			panel.ContentWidget.PushNode(rootWidget);

			host = new Host();
			host.ClientConnected += client => devices.Add(client, new RemoteDevice(client));
			host.ClientDisconnected += client => {
				if (devices.TryGetValue(client, out var device)) {
					DisconnectDevice(device);
				}
			};

			var hostButton = toolbar.AddButton("Start Host", HostButtonHandler);
			hostButton.AddChangeWatcher(() => host.IsRunning, v => hostButton.Text = v ? "Stop Host" : "Start Host");
			rootWidget.Updating += _ => UpdateDevices();

			ICommand autoStartHostingCommand;
			ICommand autoRebuildAssemblyCommand;
			ICommand savingDeviceLogsCommand;
			toolbar.AddMenuButton(
				"Options",
				new Menu {
					(autoStartHostingCommand = new Command("Automatically Start up Hosting")),
					(autoRebuildAssemblyCommand = new Command("Automatically Rebuild Assembly")),
					(savingDeviceLogsCommand = new Command("Saving Remote Device Logs")),
				},
				() => {
					autoStartHostingCommand.Checked = AutoStartUpHosting;
					autoRebuildAssemblyCommand.Checked = AutoRebuildAssembly;
					savingDeviceLogsCommand.Checked = IsSavingDeviceLogs;
				}
			);
			autoStartHostingCommand.Issued += () => AutoStartUpHosting = !AutoStartUpHosting;
			autoRebuildAssemblyCommand.Issued += () => AutoRebuildAssembly = !AutoRebuildAssembly;
			savingDeviceLogsCommand.Issued += SavingDeviceLogsCommandHandler;

			var assemblyBuilder = new AssemblyBuilder(toolbar);
			assemblyBuilder.AssemblyBuilt += assembly => rpcScrollView.Assembly = assembly;
			assemblyBuilder.AssemblyBuildFailed += () => explorableItems.SelectItem(assemblyBuilder);
			rootWidget.AddChangeWatcher(
				() => AutoRebuildAssembly,
				autoRebuildAssembly => assemblyBuilder.AutoRebuildAssembly = autoRebuildAssembly
			);
			explorableItems.AddItem(assemblyBuilder);

			ICommand disconnectCommand;
			ICommand clearCommand;
			var menu = new Menu {
				(disconnectCommand = new Command("Disconnect Device")),
				Command.MenuSeparator,
				(clearCommand = new Command("Clear Disconnected Devices"))
			};
			explorableItems.Updating += _ => {
				if (explorableItems.Input.WasKeyPressed(Key.Mouse1)) {
					explorableItems.TryGetItemUnderMouse(out var hoveredItem);
					var hoveredDevice = hoveredItem as RemoteDevice;
					if (hoveredDevice != null) {
						explorableItems.SelectItem(hoveredDevice);
					}
					disconnectCommand.Enabled = hoveredDevice != null && !hoveredDevice.WasDisconnected;
					menu.Popup();
				}
			};
			disconnectCommand.Issued += DeviceDisconnectCommandHandler;
			clearCommand.Issued += ClearDisconnectedDevicesCommandHandler;

			rpcScrollView.AddChangeWatcher(() => TryGetActiveDevice(out _), value => rpcScrollView.ItemsEnabled = value);
			rpcScrollView.RemoteProcedureCalled += (assembly, entryPoint) => {
				if (TryGetActiveDevice(out var device)) {
					device.RemoteProcedureCall(assembly.RawBytes, assembly.PdbRawBytes, entryPoint.ClassName, entryPoint.MethodName);
				}
			};

			if (AutoStartUpHosting) {
				host.Start();
			}
		}

		private void CleanUp()
		{
			host?.Stop();
			rootWidget?.UnlinkAndDispose();
			devices.Clear();
		}

		private void UpdateDevices()
		{
			foreach (var (_, device) in devices) {
				var wasInitialized = device.WasInitialized;
				device.ProcessMessages();
				if (!wasInitialized && device.WasInitialized) {
					ResetDeviceLogDirectory(device);
					explorableItems.AddItem(device);
				}
			}
		}

		private void HostButtonHandler()
		{
			if (!host.IsRunning) {
				host.Start();
			} else {
				host.Stop();
			}
		}

		private void SavingDeviceLogsCommandHandler()
		{
			string directory = null;
			if (!IsSavingDeviceLogs) {
				var dialog = new FileDialog {
					AllowedFileTypes = new[] { "" },
					Mode = FileDialogMode.SelectFolder
				};
				if (dialog.RunModal()) {
					directory = dialog.FileName;
				}
			}

			DeviceLogsDirectory = directory;
			foreach (var (_, device) in devices) {
				ResetDeviceLogDirectory(device);
			}
		}

		private void DeviceDisconnectCommandHandler()
		{
			if (TryGetActiveDevice(out var device)) {
				DisconnectDevice(device);
			}
		}

		private bool TryGetActiveDevice(out RemoteDevice device)
		{
			device = explorableItems.SelectedItem as RemoteDevice;
			return device != null && device.WasInitialized && !device.WasDisconnected;
		}

		private void DisconnectDevice(RemoteDevice device)
		{
			device.ProcessMessages();
			device.Disconnect();
			devices.Remove(device.Client);
			ResetDeviceLogDirectory(device);
		}

		private void ClearDisconnectedDevicesCommandHandler()
		{
			var devicesToRemove = new List<RemoteDevice>();
			foreach (var item in explorableItems) {
				if (item is RemoteDevice device && device.WasDisconnected) {
					devicesToRemove.Add(device);
				}
			}
			foreach (var device in devicesToRemove) {
				explorableItems.RemoveItem(device);
			}
		}

		private static void ResetDeviceLogDirectory(RemoteDevice device)
		{
			device.SetOutputDirectory(device.WasInitialized && !device.WasDisconnected ? DeviceLogsDirectory : null);
		}
	}
}
