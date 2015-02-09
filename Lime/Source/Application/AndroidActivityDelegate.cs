#if ANDROID
using System;
using Android.App;
using Android.OS;
using Android.Content.PM;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Android.Content;
using Android.Hardware;

namespace Lime
{
	public class ActivityDelegate
	{
		#region SensorListener

		/// <summary>
		/// The sensor listener used for capture accelerometer data
		/// </summary>
		class AccelerometerListener : Java.Lang.Object, ISensorEventListener
		{
			private static AccelerometerListener listener = null;

			public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
			{

			}

			public void OnSensorChanged(SensorEvent e)
			{
#if DEBUG
				if (e.Values.Count != 3) {
					throw new Lime.Exception("Invalid accelerometer data");
				}
#endif
				if (e.Values.Count == 3) { 
					Lime.Input.Acceleration = new Lime.Vector3(e.Values[0], e.Values[1], e.Values[2]);
				}

				Console.WriteLine();
			}

			public void Dispose()
			{
			}

			public static void StartListening()
			{
				if (listener == null) {
					listener = new AccelerometerListener();
					var activity = Lime.ActivityDelegate.Instance.Activity;
					var sensorManager = (SensorManager)activity.GetSystemService(Context.SensorService);
					sensorManager.RegisterListener(listener, sensorManager.GetDefaultSensor(SensorType.Accelerometer), SensorDelay.Ui);
				}
			}

			public static void StopListening()
			{
				if (listener != null) {
					var activity = Lime.ActivityDelegate.Instance.Activity;
					var sensorManager = (SensorManager)activity.GetSystemService(Context.SensorService);
					sensorManager.UnregisterListener(listener);
					listener = null;
				}
			}
		}

		#endregion

		public class BackButtonEventArgs
		{
			public bool Handled;
		}

		public delegate void BackButtonDelegate(BackButtonEventArgs args);

		public delegate void ActivityResultDelegate(int requestCode,Result resultCode,Intent data);

		public static ActivityDelegate Instance { get; private set; }

		public event Action<Activity, Bundle> Created;
		public event Action<Activity> Started;
		public event Action<Activity> Resumed;
		public event Action<Activity> Paused;
		public event Action<Activity> Stopped;
		public event Action<Activity> Destroying;
		public event Action<TrimMemory> TrimmingMemory;
		public event Action LowMemory;
		public event BackButtonDelegate BackPressed;
		public event ActivityResultDelegate ActivityResult;

		public Activity Activity { get; private set; }

		public ActivityDelegate()
		{
			Instance = this;
		}

		public virtual void OnCreate(Activity activity, Bundle bundle)
		{
			Activity = activity;
			if (Created != null) {
				Created(Activity, bundle);
			}
		}

		public virtual void OnStart()
		{
			if (Started != null) {
				Started(Activity);
			}

			AccelerometerListener.StartListening();
		}

		public virtual void OnResume()
		{
			if (Resumed != null) {
				Resumed(Activity);
			}

			AccelerometerListener.StartListening();
		}

		public virtual void OnPause()
		{
			if (Paused != null) {
				Paused(Activity);
			}

			AccelerometerListener.StopListening();
		}

		public virtual void OnStop()
		{
			if (Stopped != null) {
				Stopped(Activity);
			}

			AccelerometerListener.StopListening();           
		}

		public virtual void OnDestroy()
		{
			if (Destroying != null) {
				Destroying(Activity);
			}

			AccelerometerListener.StopListening();
			Activity = null;
		}

		public virtual void OnLowMemory()
		{
			if (LowMemory != null) {
				LowMemory();
			}
		}

		public virtual void OnTrimMemory(TrimMemory level)
		{
			if (TrimmingMemory != null) {
				TrimmingMemory(level);
			}
		}

		public virtual bool OnBackPressed()
		{
			var args = new BackButtonEventArgs();
			if (BackPressed != null) {
				BackPressed(args);
			}
			return args.Handled;
		}

		public virtual void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
			if (ActivityResult != null) {
				ActivityResult(requestCode, resultCode, data);
			}
		}
	}

	public class DefaultActivityDelegate : ActivityDelegate
	{
		public static GameView GameView { get; private set; }
		public RelativeLayout ContentView { get; private set; }
		private bool applicationCreated;
		private Application application;

		public DefaultActivityDelegate(Application app)
		{
			this.application = app;
			AudioSystem.Initialize();
		}

		public override void OnCreate(Activity activity, Bundle bundle)
		{
			if (GameView == null) {
				GameView = new GameView(activity);
			}
			Debug.Write("Activity.OnCreate");
			RemoveGameViewFromParent();
			ContentView = new RelativeLayout(activity.ApplicationContext);
			ContentView.AddView(GameView);
			activity.SetContentView(ContentView);
			GameView.Resize += (object sender, EventArgs e) => {
				// Initialize the application on Resize (not Load) event,
				// because we may need a valid screen resolution
				if (!applicationCreated) {
					applicationCreated = true;
					application.OnCreate();
				}
			};
			base.OnCreate(activity, bundle);
		}

		private void RemoveGameViewFromParent()
		{
			if (GameView.Instance.Parent != null) {
				(GameView.Instance.Parent as RelativeLayout).RemoveView(GameView);
			}
		}

		public override void OnPause()
		{
			application.Active = false;
			application.OnDeactivate();
			AudioSystem.Active = false;
			GameView.Pause();
			GameView.ClearFocus();
			base.OnPause();
		}

		public override void OnResume()
		{
			AudioSystem.Active = true;
			application.Active = true;
			application.OnActivate();
			GameView.Resume();
			if (!GameView.IsFocused) {
				GameView.RequestFocus();
			}
			GameView.Run();
			base.OnResume();
		}

		public override void OnLowMemory()
		{
			Logger.Write("Memory warning, texture memory: {0}mb", CommonTexture.TotalMemoryUsedMb);
			TexturePool.Instance.DiscardTexturesUnderPressure();
			System.GC.Collect();
			base.OnLowMemory();
		}

		public override void OnTrimMemory(TrimMemory level)
		{
			Logger.Write("Memory warning, texture memory: {0}mb", CommonTexture.TotalMemoryUsedMb);
			System.GC.Collect();
			base.OnTrimMemory(level);
		}
	}
}
#endif