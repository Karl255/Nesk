using Eto.Drawing;
using Eto.Forms;
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
		private ChannelReader<byte[]> VideoOutputChannelReader;
		private CancellationTokenSource NeskCancelSource;
		private CancellationTokenSource ReaderCancelSource;
		private bool IsRunning = false;

		public NeskWindow()
		{
			BlankBuffer = Shared.Resources.BlankBitmap;

			NeskEmu = new Nesk();

			VideoOutputChannelReader = NeskEmu.VideoOutputChannelReader;
			ReaderCancelSource = new CancellationTokenSource();

			Title = "Nesk";
			//TODO: imeplement scaling of window
			ClientSize = new Size(256, 240);

			InitMenuBar();
			InitContent();

			Closing += (s, e) =>
			{
				StopEmu();
				ReaderCancelSource.Cancel();
			};

			RunVideoReader();
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
							new ButtonMenuItem((s, e) => StartEmu()) { Text = "Run emulator" },
							new ButtonMenuItem((s, e) => StopEmu()) { Text = "Pause emulator" },
							new ButtonMenuItem((s, e) => ClearDisplay()) { Text = "Clear display" }
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

		private async Task RunVideoReader()
		{
			while (true)
			{
				DisplayBuffer = await VideoOutputChannelReader.ReadAsync(ReaderCancelSource.Token);
				RepaintDisplay();
			}
		}

		private void StartEmu()
		{
			if (!IsRunning)
			{
				IsRunning = true;
				NeskCancelSource = new CancellationTokenSource();
				Task.Run(() => NeskEmu.Run(NeskCancelSource.Token));
			}
		}

		private void StopEmu()
		{
			if (IsRunning)
			{
				IsRunning = false;
				NeskCancelSource.Cancel();
			}
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
