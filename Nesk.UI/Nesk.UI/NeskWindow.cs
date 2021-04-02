using System;
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

		public int DebugSelectedPalette = 0;
		private RadioMenuItem DebugSelectPaletteRadioController = new();

		public NeskWindow()
		{
			Title = "NESK";
			//TODO: imeplement scaling of window
			Clock = new UITimer();
			Clock.Elapsed += ClockTickHandler;

			InitMenuBar();
			InitContent();

			Content.AllowDrop = true;
			Content.DragEnter += (o, e) => e.Effects = DragEffects.All;
			Content.DragDrop += async (o, e) => await OpenROM(e.Data.Uris[0].LocalPath);

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
#if DEBUG
					// /Debug/
					new ButtonMenuItem
					{
						Text = "Debug",
						Items =
						{
							// /Debug/Show patterns
							new ButtonMenuItem((_, _) => DebugRenderPatterns(DebugSelectedPalette)) { Text = "Show patterns" },

							// /Debug/Select palette
							new ButtonMenuItem
							{
								Text = "Select palette",
								Items =
								{
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 0), DebugSelectPaletteRadioController) { Text = "Palette 0", Checked = true },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 1), DebugSelectPaletteRadioController) { Text = "Palette 1" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 2), DebugSelectPaletteRadioController) { Text = "Palette 2" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 3), DebugSelectPaletteRadioController) { Text = "Palette 3" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 4), DebugSelectPaletteRadioController) { Text = "Palette 4" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 5), DebugSelectPaletteRadioController) { Text = "Palette 5" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 6), DebugSelectPaletteRadioController) { Text = "Palette 6" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 7), DebugSelectPaletteRadioController) { Text = "Palette 7" },
								}
							}
						}
					}
#endif
				},
				// /File/Exit
				QuitItem = new ButtonMenuItem((_, _) => Application.Instance.Quit()) { Text = "&Exit" },
				// /Help/About
				AboutItem = new ButtonMenuItem() { Text = "About" } //TODO: add about info
			};
		}

		private void InitContent()
		{
			Content = new ImageView();
			Resizable = false;
			ClearDisplay();
		}

		/// <summary>
		/// Handles the <see cref="Timer.Elapsed"/> event of the <see cref="Clock"/> object by calling <see cref="Nesk.TickToNextFrame"/> and displays the generated frame.
		/// </summary>
		private async void ClockTickHandler(object sender, EventArgs e)
			=> RepaintDisplay(await Task.Run(Console.TickToNextFrame));

		/// <summary>
		/// Attempts to open the ROM at the specified path. If <paramref name="romPath"/> is omitted then a <see cref="FileDialog"/> is displayed which asks the user for the rom. The path is saved to <see cref="RomPath"/> if the ROM is valid. Automatically starts the emulation.
		/// </summary>
		private async Task OpenROM(string romPath = null)
		{
			if (romPath is null)
			{
				var dialog = new OpenFileDialog
				{
					// NOTE: crashes on linux for some reason, error message is something about "index out of range"
					CurrentFilter = new FileFilter("NES ROM files", ".nes" /*, ".unf"*/),
				};

				if (dialog.ShowDialog(this) == DialogResult.Ok)
					romPath = dialog.FileName;
				else
					return;
			}

			try
			{
				await CreateAndStartConsole(romPath);
				RomPath = romPath;
			}
			catch (Exception e)
			{
				MessageBox.Show("Failed to load ROM: " + e.Message);
			}

		}

		/// <summary>
		/// Initializes <see cref="Console"/> with a new instance of <see cref="Nesk"/> and starts the <see cref="Clock"/>.
		/// </summary>
		private async Task CreateAndStartConsole(string romPath = null)
		{
			if (romPath != null || RomPath != null)
			{
				Console = (await System.IO.File.ReadAllBytesAsync(romPath ?? RomPath))
					.ParseCartridge()
					.CreateConsole();

				Clock.Interval = 1 / Console.FrameRate;
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

		private void DebugRenderPatterns(int palette)
		{
			if (RomPath == null)
				return;
			IsRunning = false; // pause emulation
			RepaintDisplay(Console.RenderPatternMemory(palette));
		}
	}
}
