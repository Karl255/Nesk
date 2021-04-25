using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Eto.Drawing;
using Eto.Forms;
using Nesk.Shared;

namespace Nesk.UI
{
	public class NeskWindow : Form
	{
		private readonly byte[][,] BlackFrame =
		{
			new byte[256 * 1, 240 * 1],
			new byte[256 * 2, 240 * 2],
			new byte[256 * 3, 240 * 3],
			new byte[256 * 4, 240 * 4],
		};

		private byte[,] CurrentInterFrame;

		private bool CanRenderNextFrame = true;
		private readonly UITimer Clock;

		private Nesk Nes;
		private string RomPath = null;

		private CheckMenuItem PauseButton;
		private ButtonMenuItem HardResetButton;

		// key functions in order: right, left, down, up, start, select, b, a
		private readonly ImmutableArray<Keys> KeyConfig = ImmutableArray.Create(Keys.D, Keys.A, Keys.S, Keys.W, Keys.M, Keys.N, Keys.Comma, Keys.Period);
		private readonly HashSet<Keys> HeldKeys = new();

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

		private int _scale = 2;
		public int Scale
		{
			get => _scale;
			set
			{
				if (value is >= 1 and <= 4)
				{
					_scale = value;
					RepaintDisplay(CurrentInterFrame);
					ClientSize = new Size(256 * value, 240 * value);
				}
			}
		}

		private RadioMenuItem SetScaleRadioController { get; init; } = new();

		private int _debugSelectedPalette = -1;
		public int Debug_SelectedPalette
		{
			get => _debugSelectedPalette;
			set
			{
				if (value is >= -1 and <= 7)
				{
					_debugSelectedPalette = value;
					Debug_RenderPatterns();
				}
			}
		}

		private RadioMenuItem DebugSelectPaletteRadioController { get; init; } = new();

		public NeskWindow()
		{
			Title = "NESK";
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
				if (Clock != null && Nes != null)
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

					// /View/
					new ButtonMenuItem
					{
						Text = "&View",
						Items =
						{
							new ButtonMenuItem
							{
								Text = "Screen scale",
								Items =
								{
									new RadioMenuItem(new RadioCommand((_, _) => Scale = 1), SetScaleRadioController) { Text = "1x" },
									new RadioMenuItem(new RadioCommand((_, _) => Scale = 2), SetScaleRadioController) { Text = "2x", Checked = true },
									new RadioMenuItem(new RadioCommand((_, _) => Scale = 3), SetScaleRadioController) { Text = "3x" },
									new RadioMenuItem(new RadioCommand((_, _) => Scale = 4), SetScaleRadioController) { Text = "4x" },
								}
							}
						}
					},

					// /Debug/
					new ButtonMenuItem
					{
						Text = "Debug",
						Items =
						{
							// /Debug/Get next frame
							new ButtonMenuItem(async (_, _) =>
							{
								IsRunning = false;

								if (RomPath != null)
									RepaintDisplay(await Task.Run(() => Nes.TickToNextFrame()));
							}) { Text = "Get next frame", Shortcut = Keys.Control | Keys.F },

							// /Debug/Show patterns
							new ButtonMenuItem((_, _) => Debug_RenderPatterns()) { Text = "Show patterns" },

							// /Debug/Select palette
							new ButtonMenuItem
							{
								Text = "Use palette",
								Items =
								{
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = -1), DebugSelectPaletteRadioController) { Text = "None", Checked = true },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 0), DebugSelectPaletteRadioController) { Text = "Palette 0" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 1), DebugSelectPaletteRadioController) { Text = "Palette 1" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 2), DebugSelectPaletteRadioController) { Text = "Palette 2" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 3), DebugSelectPaletteRadioController) { Text = "Palette 3" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 4), DebugSelectPaletteRadioController) { Text = "Palette 4" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 5), DebugSelectPaletteRadioController) { Text = "Palette 5" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 6), DebugSelectPaletteRadioController) { Text = "Palette 6" },
									new RadioMenuItem(new RadioCommand((_, _) => Debug_SelectedPalette = 7), DebugSelectPaletteRadioController) { Text = "Palette 7" },
								}
							},

							// /Deubg/Show nametable
							new ButtonMenuItem
							{
								Text = "Show nametable",
								Items =
								{
									new ButtonMenuItem((_, _) => Debug_RenderNametable(0)) { Text = "Nametable 0" },
									new ButtonMenuItem((_, _) => Debug_RenderNametable(1)) { Text = "Nametable 1" },
									new ButtonMenuItem((_, _) => Debug_RenderNametable(2)) { Text = "Nametable 2" },
									new ButtonMenuItem((_, _) => Debug_RenderNametable(3)) { Text = "Nametable 3" },
								}
							},

							// /Debug/Dump memory to file
							new ButtonMenuItem(async(_, _) =>
							{
								if (RomPath == null)
									return;

								bool wasRunning = IsRunning;
								IsRunning = false;
								await File.WriteAllBytesAsync("memory-dump.bin", Nes.DumpMemory());
								IsRunning = wasRunning;
							}) { Text = "Dump memory to file" },

							new ButtonMenuItem((_, _) =>
							{
								if (RomPath == null)
									return;

								bool wasRunning = IsRunning;
								IsRunning = false;
								MessageBox.Show(this, string.Join(", ", Nes.Debug_DumpPaletteMemory().Select(x => x.ToString("X2"))));
								IsRunning = wasRunning;
							}) { Text = "Show palette memory" },

							// /Debug/Benchmark frame
							new ButtonMenuItem((_, _) =>
							{
								if (RomPath == null)
									return;

								bool wasRunning = IsRunning;
								IsRunning = false;

								System.Diagnostics.Stopwatch sw = new();
								sw.Start();
								byte[,] frame = Nes.TickToNextFrame();
								sw.Stop();
								RepaintDisplay(frame);
								MessageBox.Show(this, "Finished! Took " + sw.Elapsed.ToString());

								IsRunning = wasRunning;
							}) { Text = "Benchmark frame" },
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
			for (int i = 1; i <= BlackFrame.Length; i++)
			{
				BlackFrame[i - 1].FillArray<byte>(256 * i, 240 * i, 0x3f);
			}

			BackgroundColor = Color.FromArgb(0, 0, 0);
			Resizable = false;
			ClientSize = new Size(256 * Scale, 240 * Scale);
			CurrentInterFrame = BlackFrame[Scale - 1];
			Content = new ImageView();

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
			RepaintDisplay(await Task.Run(() => Nes.TickToNextFrame()));
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
				Title = "NESK | " + romPath[t..^4];
			}
			catch (Exception e)
			{
				MessageBox.Show("Failed to load ROM: " + e.Message);
			}

		}

		/// <summary>
		/// Initializes <see cref="Nes"/> with a new instance of <see cref="Nesk"/> and starts the <see cref="Clock"/>.
		/// </summary>
		private async Task CreateConsole(bool autoStart, string romPath = null)
		{
			if (romPath != null || RomPath != null)
			{
				ClearDisplay();

				Nes = (await File.ReadAllBytesAsync(romPath ?? RomPath))
					.ParseCartridge()
					.CreateConsole(ReadInput);

				Clock.Interval = 1 / Nes.FrameRate;
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
			ClearDisplay();
			await CreateConsole(true);
		}

		/// <summary>
		/// Repaint the display with the specified inter-frame buffer.
		/// </summary>
		/// <param name="interFrameBuffer">The inter-frame to be displayed - a 2D byte array of size 256x240.</param>
		private void RepaintDisplay(byte[,] interFrameBuffer)
			// TODO: check for memory leak, if present, add the following line of code:
			//(Content as ImageView).Image?.Dispose();
			=> (Content as ImageView).Image = new Bitmap(Ppu.RenderInterFrame(CurrentInterFrame = interFrameBuffer, Scale));

		/// <summary>
		///	Repaints the display with a black frame
		/// </summary>
		private void ClearDisplay() => RepaintDisplay(BlackFrame[Scale - 1]);

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

		private void Debug_RenderPatterns()
		{
			if (RomPath == null)
				return;
			IsRunning = false; // pause emulation
			RepaintDisplay(Nes.Debug_RenderPatternMemory(Debug_SelectedPalette));
		}

		private void Debug_RenderNametable(int nametable)
		{
			if (RomPath == null)
				return;
			IsRunning = false; // pause emulation
			RepaintDisplay(Nes.Debug_RenderNametable(nametable));
		}
	}
}
