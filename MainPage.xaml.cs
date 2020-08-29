using Microsoft.Gaming.XboxGameBar;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LiveSplit
{
	/// <summary>
	/// LiveSplit current time component
	/// </summary>
	public sealed partial class MainPage : Page
	{
		XboxGameBarWidget widget = null;
		DispatcherTimer dispatcherTimer;
		DispatcherTimer reconnectTimer;
		bool wsConnected = false;

		SolidColorBrush green = new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(255, 0, 204, 54));
		SolidColorBrush red = new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(255, 204, 0, 0));
		SolidColorBrush gold = new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(255, 255, 212, 0));
		SolidColorBrush current = new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(33, 255, 255, 255));
		SolidColorBrush transparent = new SolidColorBrush(Windows.UI.ColorHelper.FromArgb(0,0,0,0));

		public class Timer
        {
			public string name { get; set; }
			public double data { get; set; }
        }
		double currentTime = 0;
		public class Index
		{
			public string name { get; set; }
			public int data { get; set; }
		}
		int currentSplit = -1;
		public class Comparison
		{
			public string name { get; set; }
			public string data { get; set; }
		}
		string currentComparison = "Personal Best";
		public class Split : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;
			public int index { get; set; }
			public string icon { get; set; }
			public string name { get; set; }
			public double comparison { get; set; }
			public double splitTime { get; set; }
			public double currentTime { get; set; }
			public double personalBestSplitTime { get; set; }
			public double bestSegmentTime { get; set; }
			public double segmentSum { get; set; }
			public double delta { get; set; }
			private SolidColorBrush _color { get; set; }
			public SolidColorBrush color
			{
				get
				{
					return _color;
				}
				set
				{
					if (_color != value)
					{
						_color = value;
						if (PropertyChanged != null)
						{
							PropertyChanged(this, new PropertyChangedEventArgs("color"));
						}
					}
				}
			}
			private SolidColorBrush _background { get; set; }
			public SolidColorBrush background
			{
				get
				{
					return _background;
				}
				set
				{
					if (_background != value)
					{
						_background = value;
						if (PropertyChanged != null)
						{
							PropertyChanged(this, new PropertyChangedEventArgs("background"));
						}
					}
				}
			}
			private string _panelDelta;
			public string panelDelta
			{
				get
				{
					return _panelDelta;
				}
				set
				{
					if (_panelDelta != value)
					{
						_panelDelta = value;
						if (PropertyChanged != null)
						{
							PropertyChanged(this, new PropertyChangedEventArgs("panelDelta"));
						}
					}
				}
			}
			private string _panelTime { get; set; }
			public string panelTime
			{
				get
				{
					return _panelTime;
				}
				set
				{
					if (_panelTime != value)
					{
						_panelTime = value;
						if (PropertyChanged != null)
						{
							PropertyChanged(this, new PropertyChangedEventArgs("panelTime"));
						}
					}
				}
			}
			public void OnPropertyChanged([CallerMemberName] string name = null)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			}
		}
		public class Splits : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;
			public string name { get; set; }
			public Split[] data { get; set; }
			public void OnPropertyChanged([CallerMemberName] string name = null)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			}
		}
		public System.Collections.ObjectModel.ObservableCollection<Split> splits { get; set; }

		private MessageWebSocket messageWebSocket;
		private DataWriter messageWriter;
		private bool busy;

		public MainPage()
		{
			this.InitializeComponent();

			OnConnect();

			dispatcherTimer = new DispatcherTimer();
			dispatcherTimer.Tick += DispatcherTimer_Tick;
			dispatcherTimer.Interval = TimeSpan.FromMilliseconds(16);

			dispatcherTimer.Start();

			reconnectTimer = new DispatcherTimer();
			reconnectTimer.Tick += ReconnectTimer_Tick;
			reconnectTimer.Interval = TimeSpan.FromMilliseconds(2000);

			reconnectTimer.Start();

			this.splits = new System.Collections.ObjectModel.ObservableCollection<Split>();
			this.DataContext = this;

		}
		public static string Serialize<T>(T obj)
		{
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
			MemoryStream ms = new MemoryStream();
			serializer.WriteObject(ms, obj);
			string retVal = Encoding.UTF8.GetString(ms.ToArray());
			return retVal;
		}

		public static T Deserialize<T>(string json)
		{
			T obj = Activator.CreateInstance<T>();
			MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
			obj = (T)serializer.ReadObject(ms);
			ms.Close();
			return obj;
		}

		public static string BuildWebSocketError(Exception ex)
		{
			ex = ex.GetBaseException();

			if ((uint)ex.HResult == 0x800C000EU)
			{
				// INET_E_SECURITY_PROBLEM - our custom certificate validator rejected the request.
				return "Error: Rejected by custom certificate validation.";
			}

			WebErrorStatus status = Windows.Networking.Sockets.WebSocketError.GetStatus(ex.HResult);

			// Normally we'd use the HResult and status to test for specific conditions we want to handle.
			// In this sample, we'll just output them for demonstration purposes.
			switch (status)
			{
				case WebErrorStatus.CannotConnect:
				case WebErrorStatus.NotFound:
				case WebErrorStatus.RequestTimeout:
					return "Cannot connect to the server. Please make sure " +
						"to run the server setup script before running the sample.";

				case WebErrorStatus.Unknown:
					return "COM error: " + ex.HResult;

				default:
					return "Error: " + status;
			}
		}

		private void SetBusy(bool value)
		{
			busy = value;
		}

		private async void OnConnect()
		{
			SetBusy(true);
			await ConnectAsync();
			SetBusy(false);
		}
		private async Task ConnectAsync()
		{
			Uri server = new Uri("ws://127.0.0.1:16835/livesplit");

			messageWebSocket = new MessageWebSocket();
			messageWebSocket.Control.MessageType = SocketMessageType.Utf8;
			messageWebSocket.MessageReceived += MessageReceived;
			messageWebSocket.Closed += OnClosed;

			AppendOutputLine($"Connecting to {server}...");
			try
			{
				await messageWebSocket.ConnectAsync(server);
			}
			catch (Exception ex) // For debugging
			{
				wsConnected = false;
				// Error happened during connect operation.
				messageWebSocket.Dispose();
				messageWebSocket = null;

				AppendOutputLine(MainPage.BuildWebSocketError(ex));
				AppendOutputLine(ex.Message);

				return;
			}

			// The default DataWriter encoding is Utf8.
			messageWriter = new DataWriter(messageWebSocket.OutputStream);
			wsConnected = true;
			Help.Visibility = Visibility.Collapsed;

			Send("getcomparison");
			Send("getsplits");
		}
		async void Send(string message)
		{
			SetBusy(true);
			await SendAsync(message);
			SetBusy(false);
		}

		async Task SendAsync(string message)
		{
			// Buffer any data we want to send.
			messageWriter.WriteString(message);

			//AppendOutputLine("Sending Message: " + message);

			try
			{
				// Send the data as one complete message.
				await messageWriter.StoreAsync();
			}
			catch (Exception ex)
			{
				AppendOutputLine(MainPage.BuildWebSocketError(ex));
				AppendOutputLine(ex.Message);
				return;
			}
		}

		private string FormatTimer(double ms)
        {
			TimeSpan ts = TimeSpan.FromMilliseconds(ms);
			string time = ts.ToString(@"d\d\ hh\:mm\:ss\.ff");
			time = Regex.Replace(time, "0d ", "");
			time = time.TrimStart(new Char[] { '0', ':' });
			if (Math.Abs(ms) < 1000) time = "0" + time;
			if (ms < 0) time = "-" + time;
			return time;
		}
		private string FormatSplit(double ms)
		{
			TimeSpan ts = TimeSpan.FromMilliseconds(ms);
			string time = ts.ToString(@"d\d\ hh\:mm\:ss");
			time = Regex.Replace(time, "0d ", "");
			time = time.TrimStart(new Char[] { '0', ':' });
			if (ms < 10000) time = "0" + time;
			if (ms < 60000) time = "0:" + time;
			if (ms < 1000) time = "0:00";
			return time;
		}
		private string FormatDelta(double ms)
		{
			TimeSpan ts = TimeSpan.FromMilliseconds(ms);
			string time = ts.ToString(@"d\d\ hh\:mm\:ss\.f");
			time = Regex.Replace(time, "0d ", "");
			time = time.TrimStart(new Char[] { '0', ':' });
			if(Math.Abs(ms) > 60000)
            {
				time = time.Substring(0, time.Length - 2);
            }
			if (Math.Abs(ms) < 1000) time = "0" + time;
			if (ms < 0) time = "-" + time;
			else time = "+" + time;
			return time;
		}

		private void UpdateSplit(int i)
        {
			if (currentComparison == "Personal Best")
			{
				if (splits[i].splitTime > 0) splits[i].delta = splits[i].splitTime - splits[i].personalBestSplitTime;
				else splits[i].delta = splits[i].currentTime - splits[i].personalBestSplitTime;
				splits[i].panelTime = FormatSplit(splits[i].splitTime > 0 ? splits[i].splitTime : splits[i].personalBestSplitTime);
			}
			else if (currentComparison == "Best Segments")
			{
				if (splits[i].splitTime > 0) splits[i].delta = splits[i].splitTime - splits[i].segmentSum;
				else splits[i].delta = splits[i].currentTime - splits[i].segmentSum;
				splits[i].panelTime = FormatSplit(splits[i].splitTime > 0 ? splits[i].splitTime : splits[i].segmentSum);
			}
			if (i <= currentSplit)
				splits[i].panelDelta = FormatDelta(splits[i].delta);
			else
				splits[i].panelDelta = "";
			if (i == currentSplit)
				splits[i].background = current;
			
			splits[i].color = (splits[i].delta > 0 ? red : green);
			double segmentTime = splits[i].splitTime > 0 ? splits[i].splitTime : splits[i].currentTime;
			if (i > 0) segmentTime -= splits[i - 1].splitTime;
			if (segmentTime < splits[i].bestSegmentTime) splits[i].color = gold;
		}

		private void MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
		{
			// Dispatch the event to the UI thread so we can update UI.
			var ignore = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
			{
				using (DataReader reader = args.GetDataReader())
				{
					reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

					try
					{
						string read = reader.ReadString(reader.UnconsumedBufferLength);
						//AppendOutputLine(read);
						if (read.Contains("getcurrenttime"))
                        {
							Timer data = Deserialize<Timer>(read);
							currentTime = data.data;
							string timer = FormatTimer(data.data);
							string[] time = timer.Split('.');
							MainText.Text = time[0];
							MilliText.Text = "."+time[1];

							if (currentSplit >= 0 && currentSplit < splits.Count)
							{
								splits[currentSplit].currentTime = currentTime;
								UpdateSplit(currentSplit);
								double liveSegmentTime = 0;
								if (currentComparison == "Personal Best")
									liveSegmentTime = splits[currentSplit].delta;
								else if (currentComparison == "Best Segments")
									liveSegmentTime = currentTime - splits[currentSplit].segmentSum;
								if (currentSplit > 0) liveSegmentTime -= splits[currentSplit - 1].delta;
								LiveSegment.Text = FormatDelta(liveSegmentTime);
								LiveSegment.Foreground = (liveSegmentTime > 0 ? red : green);
							}
						}
						if (read.Contains("getsplitindex"))
						{
							Index data = Deserialize<Index>(read);
							if(currentSplit >= 0 && currentSplit < data.data)
                            {
								PreviousSegment.Text = FormatDelta(splits[currentSplit].delta);
								PreviousSegment.Foreground = splits[currentSplit].color;
							}
							if (data.data != currentSplit)
                            {
								Send("getsplits");
							}
							if (data.data < 0 && currentSplit != data.data)
							{
								PreviousSegment.Text = "";
								LiveSegment.Text = "";
								Send("getcomparison");
								Send("getsplits");
							}
							currentSplit = data.data;
						}
						if (read.Contains("getcomparison"))
						{
							Comparison data = Deserialize<Comparison>(read);
							if (currentComparison != data.data)
                            {
								Send("getsplits");
                            }
							currentComparison = data.data;
						}
						if (read.Contains("getsplits"))
						{
							Splits data = Deserialize<Splits>(read);
							splits.Clear();
							int i = 0;
							double segmentSum = 0;
							foreach(Split split in data.data)
                            {
								if (currentComparison == "Personal Best")
								{
									split.delta = split.splitTime - split.personalBestSplitTime;
									split.panelTime = FormatSplit(split.personalBestSplitTime);
								}
								else if (currentComparison == "Best Segments")
								{
									split.panelTime = FormatSplit(segmentSum + split.bestSegmentTime);
								}
								split.panelDelta = FormatDelta(split.delta);
								
								segmentSum += split.bestSegmentTime;
								split.index = i;
								split.segmentSum = segmentSum;
								i++;
								this.splits.Add(split);
							}
							i = 0;
							foreach (Split split in splits)
							{
								UpdateSplit(i);
								i++;
							}
							//AppendOutputLine(data.data.ToString());
						}
					}
					catch (Exception ex)
					{
						AppendOutputLine(MainPage.BuildWebSocketError(ex));
						AppendOutputLine(ex.Message);
					}
				}
			});
		}

		private void OnDisconnect()
		{
			wsConnected = false;
			SetBusy(true);
			CloseSocket();
			SetBusy(false);
		}

		// This may be triggered remotely by the server or locally by Close/Dispose()
		private async void OnClosed(IWebSocket sender, WebSocketClosedEventArgs args)
		{
			CloseSocket();
		}
		private void CloseSocket()
		{
			wsConnected = false;
			if (messageWriter != null)
			{
				// In order to reuse the socket with another DataWriter, the socket's output stream needs to be detached.
				// Otherwise, the DataWriter's destructor will automatically close the stream and all subsequent I/O operations
				// invoked on the socket's output stream will fail with ObjectDisposedException.
				//
				// This is only added for completeness, as this sample closes the socket in the very next code block.
				messageWriter.DetachStream();
				messageWriter.Dispose();
				messageWriter = null;
			}

			if (messageWebSocket != null)
			{
				try
				{
					messageWebSocket.Close(1000, "Closed due to user request.");
				}
				catch (Exception ex)
				{
					AppendOutputLine(MainPage.BuildWebSocketError(ex));
					AppendOutputLine(ex.Message);
				}
				messageWebSocket = null;
			}
		}
		private void AppendOutputLine(string value)
		{
			//OutputField.Text += value + "\r\n";
		}

		private void DispatcherTimer_Tick(object sender, object e)
		{
			if (!wsConnected) return;
			Send("getcurrenttime");
			Send("getsplitindex");
		}

		private void ReconnectTimer_Tick(object sender, object e)
		{
			if (!wsConnected) OnConnect();
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			// you will need access to the XboxGameBarWidget, in this case it was passed as a parameter when navigating to the widget page, your implementation may differ.
			widget = e.Parameter as XboxGameBarWidget;

			// subscribe for RequestedOpacityChanged events
			if (widget != null)
				widget.RequestedOpacityChanged += Widget_RequestedOpacityChanged;
		}

		private async void Widget_RequestedOpacityChanged(XboxGameBarWidget sender, object args)
		{
			// be sure to dispatch to the correct UI thread for this widget, Game Bar events are not guaranteed to come in on the same thread.
			await Page_Main.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				// adjust the opacity of your background as appropriate
				Background.Opacity = widget.RequestedOpacity;
			});
		}

	}
}
