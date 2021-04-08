using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
		private bool CanRenderNextFrame = true;
		private UITimer Clock { get; init; }

		private Nesk Console;
		private string RomPath = null;

		private CheckMenuItem PauseButton;
		private ButtonMenuItem HardResetButton;

		// key functions in order: right, left, down, up, start, select, b, a
		private ImmutableArray<Keys> KeyConfig { get; init; } = ImmutableArray.Create(Keys.D, Keys.A, Keys.S, Keys.W, Keys.M, Keys.N, Keys.Comma, Keys.Period);
		private HashSet<Keys> HeldKeys { get; init; } = new();

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

		public int DebugSelectedPalette = -1;
		private readonly RadioMenuItem DebugSelectPaletteRadioController = new();

		public NeskWindow()
		{
			Title = "NESK";
			//TODO: imeplement scaling of window
			Clock = new UITimer();
			Clock.Elapsed += ClockTickHandler;

			InitMenuBar();
			InitContent();

			KeyDown += (o, e) => HeldKeys.Add(e.Key);
			KeyUp += (o, e) => HeldKeys.Remove(e.Key);

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
				Enabled = false,
				Checked = true,
				Shortcut = Application.Instance.CommonModifier | Keys.P
			};

			PauseButton.CheckedChanged += (_, _) =>
			{
				if (Clock != null && Console != null)
				{
					if (!PauseButton.Checked)
						Clock.Start();
					else
					{
						Clock.Stop();
						System.Threading.Thread.Sleep(1); // make sure it stops before doing anything
					}
				}
			};

			HardResetButton = new ButtonMenuItem(async (_, _) => await HardResetEmulation())
			{
				Text = "Hard reset",
				Enabled = false
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
							HardResetButton,
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
							// /Debug/Get next frame
							new ButtonMenuItem(async (_, _) =>
							{
								if (RomPath != null)
									RepaintDisplay(await Task.Run(Console.TickToNextFrame));
							}) { Text = "Get next frame", Shortcut = Keys.Control | Keys.F },

							// /Debug/Show patterns
							new ButtonMenuItem((_, _) => DebugRenderPatterns(DebugSelectedPalette)) { Text = "Show patterns" },

							// /Debug/Select palette
							new ButtonMenuItem
							{
								Text = "Use palette",
								Items =
								{
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = -1), DebugSelectPaletteRadioController) { Text = "None", Checked = true },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 0), DebugSelectPaletteRadioController) { Text = "Palette 0" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 1), DebugSelectPaletteRadioController) { Text = "Palette 1" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 2), DebugSelectPaletteRadioController) { Text = "Palette 2" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 3), DebugSelectPaletteRadioController) { Text = "Palette 3" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 4), DebugSelectPaletteRadioController) { Text = "Palette 4" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 5), DebugSelectPaletteRadioController) { Text = "Palette 5" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 6), DebugSelectPaletteRadioController) { Text = "Palette 6" },
									new RadioMenuItem(new RadioCommand((_, _) => DebugSelectedPalette = 7), DebugSelectPaletteRadioController) { Text = "Palette 7" },
								}
							},

							// /Debug/Dump memory to file
							new ButtonMenuItem(async(_, _) =>
							{
								bool wasRunning = IsRunning;
								IsRunning = false;
								await File.WriteAllBytesAsync("memory-dump.bin", Console.DumpMemory());
								IsRunning = wasRunning;
							}) { Text = "Dump memory to file" },

							// /Debug/Benchmark frame
							new ButtonMenuItem((_, _) =>
							{
								bool wasRunning = IsRunning;
								IsRunning = false;

								System.Diagnostics.Stopwatch sw = new();
								sw.Start();
								byte[] frame = Console.TickToNextFrame();
								sw.Stop();
								RepaintDisplay(frame);
								MessageBox.Show(this, "Finished! Took " + sw.Elapsed.ToString());

								IsRunning = wasRunning;
							}) { Text = "Benchmark frame" },
						}
					},
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
		{
			if (!CanRenderNextFrame)
				return;

			CanRenderNextFrame = false;
			RepaintDisplay(await Task.Run(Console.TickToNextFrame));
			CanRenderNextFrame = true;
		}

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
				await CreateConsole(false, romPath);
				RomPath = romPath;
				int t = Math.Max(romPath.LastIndexOf('\\'), romPath.LastIndexOf('/')) + 1;
				Title = "Nesk | " + romPath[t..^4];
			}
			catch (Exception e)
			{
				MessageBox.Show("Failed to load ROM: " + e.Message);
			}

		}

		/// <summary>
		/// Initializes <see cref="Console"/> with a new instance of <see cref="Nesk"/> and starts the <see cref="Clock"/>.
		/// </summary>
		private async Task CreateConsole(bool autoStart, string romPath = null)
		{
			if (romPath != null || RomPath != null)
			{
				Console = (await File.ReadAllBytesAsync(romPath ?? RomPath))
					.ParseCartridge()
					.CreateConsole(ReadInput);

				Clock.Interval = 1 / Console.FrameRate;
				PauseButton.Enabled = true;
				HardResetButton.Enabled = true;
				if (autoStart)
					IsRunning = true;
			}
		}

		/// <summary>
		/// Stops the <see cref="Clock"/>, creates a new <see cref="Nesk"/> object and starts it.
		/// </summary>
		private async Task HardResetEmulation()
		{
			IsRunning = false;
			RepaintDisplay(BlackFrame);
			await CreateConsole(true);
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

		private uint ReadInput()
		{
			uint value = 0;

			for (int i = 0; i < 8; i++)
			{
				value <<= 1;
				value |= HeldKeys.Contains(KeyConfig[i]) ? 1u : 0u;
			}

			return value | 0xffffff00;
		}

#if DEBUG
		private void DebugRenderPatterns(int palette)
		{
			if (RomPath == null)
				return;
			IsRunning = false; // pause emulation
			RepaintDisplay(Console.RenderPatternMemory(palette));
		}
#endif
	}
}
