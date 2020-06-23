using Eto.Drawing;
using Eto.Forms;
using System;
using System.Threading.Tasks;

namespace Nesk.UI
{
	public partial class NeskWindow : Form
	{
		private readonly byte[] BlankBuffer;
		private ImageView DisplayImageView;
		private Bitmap DisplayBitmap;
		private byte[] DisplayBuffer;

		private Nesk Nesk;
		private UITimer ClockGen;

		public NeskWindow()
		{
			BlankBuffer = Resources.BlankBitmap;

			Nesk = new Nesk();
			ClockGen = new UITimer();
			ClockGen.Interval = 0.01;
			ClockGen.Elapsed += async (s, e) =>
			{
				await Task.Run(Nesk.Tick);
				if (Nesk.IsNewFrameReady)
				{
					//TODO: implement drawing the right frame
					_ = Nesk.GetFrame();
				}
				DrawRandomScreen();
			};

			Title = "Nesk";
			//TODO: imeplement scaling of window
			ClientSize = new Size(256, 240);

			InitMenuBar();
			InitContent();

		}

		private void InitMenuBar()
		{
			Menu = new MenuBar
			{
				Items =
				{
					// /file
					new ButtonMenuItem
					{
						Text = "&File",
						Items =
						{
							// /file/open rom
							new ButtonMenuItem((s,e )=> ClockGen.Start()) { Text = "Open ROM..." },
						}
					},
				},
				QuitItem = new ButtonMenuItem((s, e) => Application.Instance.Quit()) { Text = "&Exit" },
				AboutItem = new ButtonMenuItem() { Text = "About" } //TODO: add about info
			};
		}

		private void InitContent()
		{
			DisplayBuffer = (byte[])BlankBuffer.Clone();
			Content = DisplayImageView = new ImageView
			{
				Image = DisplayBitmap = new Bitmap(DisplayBuffer),
				Size = new Size(256, 240)
			};

		}

		private void RepaintDisplay()
		{
			DisplayBitmap.Dispose();
			DisplayImageView.Image = DisplayBitmap = new Bitmap(DisplayBuffer);
		}

		private void ResetDisplay()
		{
			DisplayBuffer = (byte[])BlankBuffer.Clone();
			RepaintDisplay();
		}

		public void DrawRandomScreen()
		{
			int start = DisplayBuffer[0x0A];
			var rand = new Random();

			for (int y = 0; y < 240; y++)
			{
				for (int x = 0; x < 256; x++)
				{
					DisplayBuffer[start + (y * 256 + x) * 3 + 0] = (byte)rand.Next();
					DisplayBuffer[start + (y * 256 + x) * 3 + 1] = (byte)rand.Next();
					DisplayBuffer[start + (y * 256 + x) * 3 + 2] = (byte)rand.Next();
				}
			}

			RepaintDisplay();
		}
	}
}
