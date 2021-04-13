using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lime;
using RemoteScripting;
using RemoteScriptingHost = RemoteScripting.Host;
using Task = System.Threading.Tasks.Task;

namespace Tangerine.UI.RemoteScripting
{
	public class Host : IDisposable
	{
		private CancellationTokenSource cancellationTokenSource;
		private CancellationToken cancellationToken;

		public bool IsRunning { get; private set; }

		public delegate void ClientConnectedDelegate(HostClient client);
		public ClientConnectedDelegate ClientConnected;
		public delegate void ClientDisconnectedDelegate(HostClient client);
		public ClientDisconnectedDelegate ClientDisconnected;

		public void Start()
		{
			Application.Exited += Stop;
			cancellationTokenSource = new CancellationTokenSource();
			cancellationToken = cancellationTokenSource.Token;
			Hosting();

			async void Hosting()
			{
				IsRunning = true;
				RemoteScriptingHost host = null;
				var clients = new HashSet<HostClient>();
				try {
					host = new RemoteScriptingHost(cancellationTokenSource);
					host.Start();
					var clientsToRemove = new List<HostClient>();
					while (!cancellationToken.IsCancellationRequested) {
						foreach (var client in host.Clients) {
							if (client.WasVerified && !clients.Contains(client)) {
								clients.Add(client);
								ClientConnected?.Invoke(client);
							}
						}
						foreach (var client in clients) {
							if (!client.IsConnected) {
								clientsToRemove.Add(client);
								ClientDisconnected?.Invoke(client);
							}
						}
						if (clientsToRemove.Count > 0) {
							foreach (var client in clientsToRemove) {
								clients.Remove(client);
							}
							clientsToRemove.Clear();
						}
						const int UpdateDelay = 1000 / 20;
						await Task.Delay(UpdateDelay, cancellationToken);
					}
				} catch (ObjectDisposedException) {
					// Suppress
				} catch (TaskCanceledException) {
					// Suppress
				} catch (System.Exception exception) {
					// Suppress
					System.Console.WriteLine(exception);
				} finally {
					host?.Stop();
					foreach (var client in clients) {
						ClientDisconnected?.Invoke(client);
					}
					IsRunning = false;
					cancellationTokenSource = null;
				}
			}
		}

		public void Stop()
		{
			Application.Exited -= Stop;
			cancellationTokenSource?.Cancel();
		}

		public void Dispose() => Stop();
	}
}
