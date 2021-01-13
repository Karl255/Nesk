using System;
using System.Threading.Tasks;
using System.Timers;
using Eto.Drawing;
using Eto.Forms;

namespace Nesk.UI
{
	public class NeskWindow : Form
	{
		private readonly byte[] BlackFrame = Shared.Resources.BlankBitmap;
		private UITimer Clock;
		private Nesk Console;
		private string RomPath = null;

		private CheckMenuItem PauseButton;
		private bool _isRunning = false;
		private bool IsRunning
		{
			get => _isRunning;
			set
			{
				if (Clock != null)
				{
					_isRunning = value;
					PauseButton.Checked = !value;
					if (value)
						Clock.Start();
					else
						Clock.Stop();
				}
			}
		}

		public NeskWindow()
		{
			Title = "NESK";
			//TODO: imeplement scaling of window
			ClientSize = new Size(256, 240);

			InitMenuBar();
			InitContent();

			Closing += (_, _) => IsRunning = false;
		}

		private void InitMenuBar()
		{
			PauseButton = new CheckMenuItem()
			{
				Text = "Pause",
				Shortcut = Application.Instance.CommonModifier | Keys.P
			};
			PauseButton.CheckedChanged += (_, _) => TogglePause();

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
							new ButtonMenuItem((_, _) => OpenROM()) { Text = "Open ROM..." },
							new ButtonMenuItem((_, _) => HardResetEmulation()) { Text = "Hard reset" },
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
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void ClockTickHandler(object sender, EventArgs e)
			=> RepaintDisplay(await Task.Run(Console.TickToNextFrame));

		/// <summary>
		/// Opens an open file dialog for selecting the ROM file. Automatically starts the emulation.
		/// </summary>
		private void OpenROM()
		{
			var dialog = new OpenFileDialog
			{
				// NOTE: crashes on linux for some reason, error message is something about "index out of range"
				CurrentFilter = new FileFilter("NES ROM files", ".nes" /*, ".unf"*/),
			};

			if (dialog.ShowDialog(this) == DialogResult.Ok)
			{
				RomPath = dialog.FileName;
				CreateAndStartConsole();
			}
		}

		/// <summary>
		/// Initializes <see cref="Console"/> with a new instance of <see cref="Nesk"/> and starts the <see cref="Clock"/>.
		/// </summary>
		private void CreateAndStartConsole()
		{
			if (RomPath != null)
			{
				// TODO: properly implement this
				Console = new Nesk();
				Clock = new UITimer() { Interval = 1 / Console.FrameRate };
				Clock.Elapsed += ClockTickHandler;
				IsRunning = true;
			}
		}

		/// <summary>
		/// Stops the <see cref="Clock"/>, creates a new <see cref="Nesk"/> object and starts it.
		/// </summary>
		private void HardResetEmulation()
		{
			IsRunning = false;
			CreateAndStartConsole();
		}

		/// <summary>
		/// Toggles the running state of the emulation.
		/// </summary>
		private void TogglePause()
		{
			if (Clock != null)
				IsRunning = !IsRunning;
		}

		/// <summary>
		/// Repaint the display with the specified frame buffer.
		/// </summary>
		/// <param name="frameBuffer">Array of bytes containing the bitmap image data of the frame.</param>
		private void RepaintDisplay(byte[] frameBuffer)
			// TODO: check for memory leak, if present, add the following line of code:
			//content.Image.Dispose();
			=> (Content as ImageView).Image = new Bitmap(frameBuffer);

		/// <summary>
		///	Repaints the display with a black frame
		/// </summary>
		private void ClearDisplay() => RepaintDisplay(BlackFrame.Clone() as byte[]);
	}
}
