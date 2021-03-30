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
					device.ProcessMessages();
					device.Disconnect();
					devices.Remove(client);
					ResetDeviceLogDirectory(device);
				}
			};

			var hostButton = toolbar.AddButton("Start Host", HostButtonHandler);
			hostButton.AddChangeWatcher(() => host.IsRunning, v => hostButton.Text = v ? "Stop Host" : "Start Host");
			rootWidget.Updating += _ => UpdateDevices();

			ICommand savingDeviceLogsCommand;
			toolbar.AddMenuButton(
				"Options",
				new Menu {
					(savingDeviceLogsCommand = new Command("Saving remote device logs")),
				},
				() => savingDeviceLogsCommand.Checked = IsSavingDeviceLogs
			);
			savingDeviceLogsCommand.Issued += SavingDeviceLogsCommandHandler;

			explorableItems.AddItem(new AssemblyBuilder(toolbar));

			rpcScrollView.AddChangeWatcher(() => TryGetActiveDevice(out _), value => rpcScrollView.ItemsEnabled = value);
			rpcScrollView.RemoteProcedureCalled += (assembly, entryPoint) => {
				if (TryGetActiveDevice(out var device)) {
					device.RemoteProcedureCall(assembly.RawBytes, assembly.PdbRawBytes, entryPoint.ClassName, entryPoint.MethodName);
				}
			};

			bool TryGetActiveDevice(out RemoteDevice device)
			{
				device = explorableItems.SelectedItem as RemoteDevice;
				return device != null && device.WasInitialized && !device.WasDisconnected;
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

		private static void ResetDeviceLogDirectory(RemoteDevice device)
		{
			device.SetOutputDirectory(device.WasInitialized && !device.WasDisconnected ? DeviceLogsDirectory : null);
		}
	}
}
