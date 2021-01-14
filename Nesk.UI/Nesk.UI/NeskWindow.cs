using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using Eto.Drawing;
using Eto.Forms;
using Nesk.Shared;

namespace Nesk.UI
{
	public class NeskWindow : Form
	{
		private readonly byte[] BlackFrame = Shared.Resources.BlankBitmap;
		private UITimer Clock { get; init; }
		private Nesk Console;
		private string RomPath = null;

		private CheckMenuItem PauseButton;

		private bool IsRunning
		{
			get => !PauseButton.Checked;

			set
			{
				// setting this property to true (value = false) triggers the event handler which calls Clock.Stop()
				// but this is not the case when setting it to false (value = true) so Clock.Start() needs to be called manually
				PauseButton.Checked = !value;

				if (value)
					Clock.Start();
			}
		}

		public NeskWindow()
		{
			Title = "NESK";
			//TODO: imeplement scaling of window
			ClientSize = new Size(256, 240);
			Clock = new UITimer();
			Clock.Elapsed += ClockTickHandler;

			InitMenuBar();
			InitContent();

			Closing += (_, _) => Clock.Stop();
		}

		private void InitMenuBar()
		{
			PauseButton = new()
			{
				Text = "Pause",
				Shortcut = Application.Instance.CommonModifier | Keys.P
			};

			PauseButton.CheckedChanged += (_, _) =>
			{
				if (Clock != null && Console != null)
				{
					if (!PauseButton.Checked)
						Clock.Start();
					else
						Clock.Stop();
				}
			};

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
							new ButtonMenuItem(async (_, _) => await OpenROM()) { Text = "Open ROM..." },
							new ButtonMenuItem(async (_, _) => await HardResetEmulation()) { Text = "Hard reset" },
							PauseButton
						}
					},
				},
				// /File/Exit
				QuitItem = new ButtonMenuItem((_, _) => Application.Instance.Quit()) { Text = "&Exit" },
				// /Help/About
				AboutItem = new ButtonMenuItem() { Text = "About" } //TODO: add about info
			};
		}

		private void InitContent()
		{
			Content = new ImageView { Size = new Size(256, 240) };
			ClearDisplay();
		}

		/// <summary>
		/// Handles the <see cref="Timer.Elapsed"/> event of the <see cref="Clock"/> object by calling <see cref="Nesk.TickToNextFrame"/> and displays the generated frame.
		/// </summary>
		private async void ClockTickHandler(object sender, EventArgs e)
			=> RepaintDisplay(await Task.Run(Console.TickToNextFrame));

		/// <summary>
		/// Opens an open file dialog for selecting the ROM file. Automatically starts the emulation.
		/// </summary>
		private async Task OpenROM()
		{
			var dialog = new OpenFileDialog
			{
				// NOTE: crashes on linux for some reason, error message is something about "index out of range"
				CurrentFilter = new FileFilter("NES ROM files", ".nes" /*, ".unf"*/),
			};

			if (dialog.ShowDialog(this) == DialogResult.Ok)
			{
				RomPath = dialog.FileName;
				await CreateAndStartConsole();
				Clock.Interval = 1 / Console.FrameRate;
			}
		}

		/// <summary>
		/// Initializes <see cref="Console"/> with a new instance of <see cref="Nesk"/> and starts the <see cref="Clock"/>.
		/// </summary>
		private async Task CreateAndStartConsole()
		{
			if (RomPath != null)
			{
				Console = (await System.IO.File.ReadAllBytesAsync(RomPath))
					.ParseCartridge()
					.CreateConsole();

				IsRunning = true;
			}
		}

		/// <summary>
		/// Stops the <see cref="Clock"/>, creates a new <see cref="Nesk"/> object and starts it.
		/// </summary>
		private async Task HardResetEmulation()
		{
			IsRunning = false;
			await CreateAndStartConsole();
		}

		/// <summary>
		/// Repaint the display with the specified frame buffer.
		/// </summary>
		/// <param name="frameBuffer">Array of bytes containing the bitmap image data of the frame.</param>
		private void RepaintDisplay(byte[] frameBuffer)
			// TODO: check for memory leak, if present, add the following line of code:
			//(Content as ImageView).Image?.Dispose();
			=> (Content as ImageView).Image = new Bitmap(frameBuffer);

		/// <summary>
		///	Repaints the display with a black frame
		/// </summary>
		private void ClearDisplay() => RepaintDisplay(BlackFrame.CloneArray());
	}
}
