using Eto.Drawing;
using Eto.Forms;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nesk.UI
{
	public class NeskWindow : Form
	{
		private readonly byte[] BlankBuffer;
		private ImageView DisplayImageView;
		private Bitmap DisplayBitmap;
		private byte[] DisplayBuffer;

		private Nesk NeskEmu;
		private ChannelReader<byte[]> VideoOutputChannelReader;
		private CancellationTokenSource NeskCancelSource;
		private CancellationTokenSource VideoReaderCancelSource;
		private bool IsRunning = false;
		private string ROMPath = null;

		public NeskWindow()
		{
			BlankBuffer = Shared.Resources.BlankBitmap;

			Title = "Nesk";
			//TODO: imeplement scaling of window
			ClientSize = new Size(256, 240);
			
			InitMenuBar();
			InitContent();

			Closing += (s, e) =>
			{
				StopEmulation();
				VideoReaderCancelSource?.Cancel();
			};
		}

		private void InitMenuBar()
		{
			Menu = new MenuBar
			{
				Items =
				{
					// /File/
					new ButtonMenuItem
					{
						Text = "&File",
						Items =
						{
							new ButtonMenuItem((s, e) => OpenROM()) { Text = "Open ROM..." },
							new ButtonMenuItem((s, e) => TogglePause()) { Text = "Pause" }
						}
					},
				},
				// /File/Exit
				QuitItem = new ButtonMenuItem((s, e) => Application.Instance.Quit()) { Text = "&Exit" },
				// /Help/About
				AboutItem = new ButtonMenuItem() { Text = "About" } //TODO: add about info
			};
		}

		private void InitContent()
		{
			DisplayBuffer = (byte[])BlankBuffer.Clone();
			Content = DisplayImageView = new ImageView
			{
				Image = DisplayBitmap = new Bitmap(DisplayBuffer),
				Size = new Size(256, 240),
				
			};

		}

		private async Task RunVideoReaderAsync()
		{
			var token = VideoReaderCancelSource.Token;
			while (!token.IsCancellationRequested)
			{
				DisplayBuffer = await VideoOutputChannelReader.ReadAsync(VideoReaderCancelSource.Token);
				RepaintDisplay();
			}
		}

		private void OpenROM()
		{
			var d = new OpenFileDialog
			{
				CurrentFilter = new FileFilter("NES ROM files", ".nes", ".unf")
			};

			if (d.ShowDialog(this) == DialogResult.Ok)
			{
				ROMPath = d.FileName;
				ResetEmulationHard();
			}
		}

		private void ResetEmulationHard()
		{
			if (ROMPath is null)
				return;

			if (IsRunning)
				StopEmulation();

			NeskEmu = new Nesk(ROMPath);
			VideoOutputChannelReader = NeskEmu.VideoOutputChannelReader;
			VideoReaderCancelSource = new CancellationTokenSource();

			RunVideoReaderAsync();
			StartEmulation();
		}

		private void StartEmulation()
		{
			if (IsRunning)
				return;

			IsRunning = true;
			NeskCancelSource = new CancellationTokenSource();
			Task.Run(() => NeskEmu.RunAsync(NeskCancelSource.Token));
		}

		private void StopEmulation()
		{
			if (!IsRunning)
				return;

			IsRunning = false;
			NeskCancelSource.Cancel();
		}

		private void TogglePause()
		{
			//if no rom is selected (this is the case only after the program starts)
			if (ROMPath is null)
				return;

			if (IsRunning)
				StopEmulation();
			else
				StartEmulation();
		}

		private void RepaintDisplay()
		{
			DisplayBitmap.Dispose();
			DisplayImageView.Image = DisplayBitmap = new Bitmap(DisplayBuffer);
		}

		private void ClearDisplay()
		{
			DisplayBuffer = (byte[])BlankBuffer.Clone();
			RepaintDisplay();
		}
	}
}
