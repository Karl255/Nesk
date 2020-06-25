using Eto.Drawing;
using Eto.Forms;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nesk.UI
{
	public partial class NeskWindow : Form
	{
		private readonly byte[] BlankBuffer;
		private ImageView DisplayImageView;
		private Bitmap DisplayBitmap;
		private byte[] DisplayBuffer;

		private Nesk NeskEmu;
		private ChannelReader<byte[]> DataChannelReader;
		private CancellationTokenSource CancelSource;

		public NeskWindow()
		{
			BlankBuffer = Shared.Resources.BlankBitmap;

			NeskEmu = new Nesk();
			DataChannelReader = NeskEmu.VideoOutputChannelReader;
			CancelSource = new CancellationTokenSource();

			Title = "Nesk";
			//TODO: imeplement scaling of window
			ClientSize = new Size(256, 240);

			InitMenuBar();
			InitContent();

			Closing += (s, e) =>
			{
				CancelSource.Cancel();
			};

			RunEmu();
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
							new ButtonMenuItem((s, e) => NeskEmu.Start()) { Text = "Run emulator" },
							new ButtonMenuItem((s, e) => NeskEmu.Stop()) { Text = "Pause emulator" }
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

		private async Task RunEmu()
		{
			while (true)
			{
				DisplayBuffer = await DataChannelReader.ReadAsync(CancelSource.Token);
				RepaintDisplay();
			}
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
	}
}
