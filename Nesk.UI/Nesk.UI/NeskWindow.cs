using Eto.Drawing;
using Eto.Forms;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nesk.UI
{
	public class NeskWindow : Form
	{
		private readonly byte[] BlankBuffer = Shared.Resources.BlankBitmap;
		private ImageView DisplayImageView;
		private Bitmap DisplayBitmap;
		private byte[] DisplayBuffer;

		private Nesk NeskEmu;
		private ChannelReader<byte[]> VideoOutputChannelReader;
		private CancellationTokenSource NeskCancelSource;
		private CancellationTokenSource VideoReaderCancelSource;
		private string ROMPath = null;

		public NeskWindow()
		{
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

		/// <summary>
		/// This method waits until the next frame is ready and when it is, it repaints the display with it and starts over. Runs permanently, until canceled using VideoReaderCancelSource.
		/// </summary>
		private async void RunVideoReaderAsync()
		{
			var token = VideoReaderCancelSource.Token;
			try
			{
				while (!token.IsCancellationRequested)
				{
					RepaintDisplay(await VideoOutputChannelReader.ReadAsync(VideoReaderCancelSource.Token));
				}
			}
			catch (OperationCanceledException) { }
		}

		/// <summary>
		/// Opens a dialog window in which the user selects the ROM file. Automatically hard restarts the emulation on successful file choice.
		/// </summary>
		private void OpenROM()
		{
			var dialog = new OpenFileDialog
			{
				CurrentFilter = new FileFilter("NES ROM files", ".nes" /*, ".unf"*/)
			};

			if (dialog.ShowDialog(this) == DialogResult.Ok)
			{
				ClearDisplay();
				ROMPath = dialog.FileName;
				ResetEmulationHard();
			}
		}

		private void CreateEmulator()
		{
			if (NeskEmu != null)
			{
				VideoReaderCancelSource.Cancel();
				VideoReaderCancelSource.Dispose();
				// in the future, call NeskEmu.Dispose() if it ever gets added
			}

			NeskEmu = new Nesk(ROMPath);
			VideoOutputChannelReader = NeskEmu.VideoOutputChannelReader;
			VideoReaderCancelSource = new CancellationTokenSource();
			RunVideoReaderAsync();
		}

		/// <summary>
		/// Stops the emulation, creates a new Nesk object and starts that new one.
		/// </summary>
		private void ResetEmulationHard()
		{
			if (string.IsNullOrEmpty(ROMPath))
				return;

			if (NeskEmu?.IsRunning ?? false)
				StopEmulation();

			CreateEmulator();
			StartEmulation();
		}

		/// <summary>
		/// Starts the emulation if it isn't already running.
		/// </summary>
		private void StartEmulation()
		{
			if (NeskEmu.IsRunning)
				return;

			NeskCancelSource = new CancellationTokenSource();
			Task.Run(() => NeskEmu.RunAsync(NeskCancelSource.Token)); // NOTE: the Task.Run is needed
		}

		/// <summary>
		/// Stops the emulation if it's running.
		/// </summary>
		private void StopEmulation()
		{
			if (NeskEmu != null && NeskEmu.IsRunning)
				NeskCancelSource.Cancel();
		}

		/// <summary>
		/// Toggles the running state of the emulation.
		/// </summary>
		private void TogglePause()
		{
			//if no rom is selected (this is the case only after the program starts)
			if (string.IsNullOrEmpty(ROMPath))
				return;

			if (NeskEmu.IsRunning)
				StopEmulation();
			else
				StartEmulation();
		}

		/// <summary>
		/// Repaint the display with the specified frame buffer.
		/// </summary>
		/// <param name="frameBuffer">Array of bytes containing the bitmap image data of the frame.</param>
		private void RepaintDisplay(byte[] frameBuffer)
		{
			DisplayBitmap.Dispose();
			DisplayImageView.Image = DisplayBitmap = new Bitmap(frameBuffer);
		}

		/// <summary>
		///	Repaints the display with a blank buffer (black screen).
		/// </summary>
		private void ClearDisplay() => RepaintDisplay((byte[])BlankBuffer.Clone());
	}
}
