using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteScripting
{
	public class Host
	{
		private readonly TcpListener listener;
		private readonly CancellationTokenSource abortTokenSource;
		private readonly CancellationTokenSource cancellationTokenSource;
		private readonly CancellationToken cancellationToken;
		private readonly ConcurrentDictionary<HostClient, object> clients =
			new ConcurrentDictionary<HostClient, object>();
		public IEnumerable<HostClient> Clients => clients.Keys.Where(c => c.WasVerified);

		public Host(CancellationTokenSource abortTokenSource)
		{
			listener = new TcpListener(IPAddress.Any, NetworkSettings.Port);
			this.abortTokenSource = abortTokenSource;
			cancellationTokenSource = new CancellationTokenSource();
			cancellationToken = cancellationTokenSource.Token;
		}

		public async void Start()
		{
			listener.Start();

			while (!cancellationToken.IsCancellationRequested) {
				try {
					var tcpClient = await listener.AcceptTcpClientAsync()
						.ConfigureAwait(continueOnCapturedContext: false);
					CreateClient(tcpClient);
				} catch (ObjectDisposedException) {
					// Suppress
					Abort();
				} catch (Exception exception) {
					Abort(exception);
				}
			}

			foreach (var client in clients.Keys) {
				client.Close();
			}
		}

		private async void CreateClient(TcpClient tcpClient)
		{
			var client = new HostClient(tcpClient);

			try {
				if (!clients.TryAdd(client, null)) {
					throw new InvalidOperationException();
				}
				await client.ProcessConnectionAsync().ConfigureAwait(continueOnCapturedContext: false);
			} catch (System.IO.IOException exception) {
				// Suppress
				EnqueueClientException(exception);
			} catch (ObjectDisposedException exception) {
				// Suppress
				EnqueueClientException(exception);
			} catch (TaskCanceledException exception) {
				// Suppress
				EnqueueClientException(exception);
			} catch (NetworkException exception) {
				// Suppress
				EnqueueClientException(exception);
			} catch (Exception exception) {
				Abort(exception);
			} finally {
				if (!clients.TryRemove(client, out _)) {
					throw new InvalidOperationException();
				}
				client.Close();
			}

			void EnqueueClientException(Exception exception)
			{
				client.EnqueueReceivedMessage(new NetworkText($"\nClient disconnected with exception:\n{exception}"));
			}
		}

		public void Stop()
		{
			cancellationTokenSource.Cancel();
			listener.Stop();
		}

		private void Abort(Exception exception = null)
		{
			abortTokenSource.Cancel();
			Stop();
			if (exception != null) {
				Console.WriteLine(exception);
			}
		}
	}
}
